require('dotenv').config();
const express = require('express');
const mongoose = require('mongoose');
const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');
const nodemailer = require('nodemailer');
const cors = require('cors');
const os = require('os');
const fs = require('fs');
const path = require('path');
const { exec, spawn } = require('child_process');
const User = require('./models/User');
const Room = require('./models/Room');
const Admin = require('./models/Admin');
const AdminLog = require('./models/AdminLog');


const app = express();
const PORT = process.env.PORT || 3000;

// ─── System Metrics & Traffic Tracking ────────────────────────────────────────
const networkStats = {
    totalRequests: 0,
    totalBytesRx: 0,
    totalBytesTx: 0,
    activeConnections: 0,
    recentRequestTimestamps: []
};

// Middleware theo dõi lưu lượng mạng và số lượng Request
app.use((req, res, next) => {
    networkStats.totalRequests++;
    networkStats.activeConnections++;
    const now = Date.now();
    networkStats.recentRequestTimestamps.push(now);

    // Xóa các mốc thời gian quá 10 giây trước để tính RPS
    const tenSecAgo = now - 10000;
    while (networkStats.recentRequestTimestamps.length > 0 && networkStats.recentRequestTimestamps[0] < tenSecAgo) {
        networkStats.recentRequestTimestamps.shift();
    }

    // Ước tính dung lượng Request gửi lên
    const reqSize = Number(req.headers['content-length']) || (req.url.length + 100);
    networkStats.totalBytesRx += reqSize;

    const originalEnd = res.end;
    res.end = function (chunk, encoding) {
        if (chunk) {
            networkStats.totalBytesTx += Buffer.isBuffer(chunk) ? chunk.length : Buffer.byteLength(chunk || '', encoding);
        }
        networkStats.activeConnections = Math.max(0, networkStats.activeConnections - 1);
        originalEnd.apply(res, arguments);
    };

    next();
});

// Hàm tính toán CPU % theo độ lệch thời gian (Delta Sampling)
let previousCpuTimes = null;
function calculateCpuUsage() {
    const cpus = os.cpus();
    if (!cpus || cpus.length === 0) {
        return { usagePercent: 0, cores: 1, model: 'Unknown', speed: 0 };
    }

    let totalIdle = 0;
    let totalTick = 0;
    for (const cpu of cpus) {
        for (const type in cpu.times) {
            totalTick += cpu.times[type];
        }
        totalIdle += cpu.times.idle;
    }

    let usagePercent = 0;
    if (previousCpuTimes) {
        const idleDelta = totalIdle - previousCpuTimes.idle;
        const totalDelta = totalTick - previousCpuTimes.total;
        if (totalDelta > 0) {
            const rawPercent = (1 - (idleDelta / totalDelta)) * 100;
            usagePercent = Math.max(0, Math.min(100, Math.round(rawPercent * 10) / 10));
        }
    }
    previousCpuTimes = { idle: totalIdle, total: totalTick };

    return {
        usagePercent,
        cores: cpus.length,
        model: cpus[0].model,
        speed: cpus[0].speed
    };
}

// Khởi tạo mẫu CPU ban đầu
calculateCpuUsage();
// Cập nhật mẫu CPU liên tục mỗi giây để luôn có delta chính xác khi client request
setInterval(calculateCpuUsage, 1000);

// Hàm lấy thông tin RAM chuẩn cho Linux (tránh hiểu lầm Buffer/Cache là RAM đã dùng)
function getSystemMemory() {
    const totalMem = os.totalmem();
    let freeMem = os.freemem();

    // Trên Linux, đọc /proc/meminfo để lấy MemAvailable chính xác (đã trừ buffer/cache)
    if (process.platform === 'linux') {
        try {
            const meminfo = fs.readFileSync('/proc/meminfo', 'utf8');
            const match = meminfo.match(/MemAvailable:\s+(\d+)\s+kB/i);
            if (match && match[1]) {
                freeMem = parseInt(match[1], 10) * 1024;
            }
        } catch (e) {
            // fallback sang os.freemem()
        }
    }

    const usedMem = Math.max(0, totalMem - freeMem);
    const memUsagePercent = Math.max(0, Math.min(100, Math.round((usedMem / totalMem) * 1000) / 10));

    return {
        totalBytes: totalMem,
        usedBytes: usedMem,
        freeBytes: freeMem,
        usagePercent: memUsagePercent
    };
}

// Hàm lấy DB Ping có bộ đệm Cache 10s tránh spam lệnh tới MongoDB Atlas
let cachedDbPing = 0;
let lastDbPingTime = 0;
async function getDatabasePing() {
    const now = Date.now();
    if (now - lastDbPingTime < 10000 && lastDbPingTime > 0) {
        return cachedDbPing;
    }

    if (mongoose.connection.readyState === 1) {
        const start = Date.now();
        try {
            await mongoose.connection.db.admin().ping();
            cachedDbPing = Date.now() - start;
            lastDbPingTime = now;
            return cachedDbPing;
        } catch (e) {
            cachedDbPing = -1;
            return -1;
        }
    }
    return -1;
}

// ─── Middleware ───────────────────────────────────────────────────────────────
app.use(cors());
app.use(express.json());

// ─── MongoDB ──────────────────────────────────────────────────────────────────
mongoose.connect(process.env.MONGO_URI)
    .then(() => {
        console.log('[DB] MongoDB Atlas connected');
        // Tự động đồng bộ các index để xóa bỏ những index cũ (như unique index của host cũ)
        Room.syncIndexes()
            .then(() => console.log('[DB] Room indexes synced successfully'))
            .catch(err => console.error('[DB] Room index sync error:', err));
        
        // Tự động nạp dữ liệu (seed) 5 admin mặc định nếu chưa tồn tại
        seedAdmins()
            .then(() => console.log('[DB] Admin seeding check complete'))
            .catch(err => console.error('[DB] Admin seeding error:', err));
    })
    .catch(err => { console.error('[DB] Connection error:', err); process.exit(1); });

// ─── Nodemailer ───────────────────────────────────────────────────────────────
const transporter = nodemailer.createTransport({
    service: 'gmail',
    auth: {
        user: process.env.EMAIL_USER,
        pass: process.env.EMAIL_PASS
    }
});

// ─── Helpers ──────────────────────────────────────────────────────────────────
async function seedAdmins() {
    try {
        const adminCount = await Admin.countDocuments();
        if (adminCount === 0) {
            console.log('[DB] Seeding default 5 admin accounts...');
            const defaultAdmins = [
                { username: 'hoaibao', displayName: 'Nguyễn Mạnh Hoài Bảo', role: 'superadmin' },
                { username: 'duytan', displayName: 'Nguyễn Duy Tân', role: 'admin' },
                { username: 'nhatdong', displayName: 'Nhật Đông', role: 'admin' },
                { username: 'huuhoang', displayName: 'Hồ Hữu Hoàng', role: 'admin' },
                { username: 'luanvu', displayName: 'Vũ Phạm Luân', role: 'superadmin' }
            ];

            // Default password: 123456
            const defaultPasswordHash = await bcrypt.hash('123456', 12);

            for (const adminData of defaultAdmins) {
                const admin = new Admin({
                    username: adminData.username,
                    displayName: adminData.displayName,
                    passwordHash: defaultPasswordHash,
                    isFirstLogin: true,
                    role: adminData.role
                });
                await admin.save();
            }
            console.log('[DB] Seeding completed. 5 admin accounts initialized.');
        } else {
            // Tự động di trú vai trò (migration) cho database đã tồn tại
            await Admin.updateMany({ username: { $in: ['hoaibao', 'luanvu'] } }, { role: 'superadmin' });
            await Admin.updateMany({ username: { $in: ['duytan', 'nhatdong', 'huuhoang'] } }, { role: 'admin' });
            console.log('[DB] Automatic role migration executed.');
        }
    } catch (err) {
        console.error('[DB] Seeding/Migration failed:', err);
    }
}

function generateOTP() {
    return Math.floor(100000 + Math.random() * 900000).toString();
}

async function sendOtpEmail(email, displayName, otp) {
    const mailOptions = {
        from: `"${process.env.EMAIL_FROM_NAME || 'Atlantis Deep Slumber'}" <${process.env.EMAIL_USER}>`,
        to: email,
        subject: '🌊 Mã xác nhận Atlantis: Deep Slumber',
        html: `
<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <style>
    body { background: #000408; color: #a0e6ff; font-family: 'Courier New', monospace; margin: 0; padding: 0; }
    .container { max-width: 480px; margin: 40px auto; background: rgba(2,9,16,0.95); border: 1px solid rgba(0,190,255,0.4); border-left: 4px solid #00beff; border-radius: 4px 16px 4px 16px; padding: 36px 40px; text-align: center; }
    .title { color: #00e8ff; font-size: 22px; letter-spacing: 4px; margin-bottom: 8px; }
    .subtitle { color: #5080a0; font-size: 12px; letter-spacing: 2px; margin-bottom: 28px; }
    .welcome-text { color: #ffffff; font-size: 16px; margin-bottom: 10px; }
    .instruction-text { color: #3cdcff; font-size: 14px; margin-bottom: 20px; }
    .otp-box { background: rgba(0,190,255,0.08); border: 1px solid rgba(0,190,255,0.5); border-radius: 6px; text-align: center; padding: 20px; margin: 24px 0; }
    .otp-code { font-size: 42px; font-weight: bold; letter-spacing: 14px; color: #00e8ff; text-shadow: 0 0 20px rgba(0,230,255,0.7); }
    .ttl { color: #5080a0; font-size: 12px; margin-top: 8px; }
    .footer { color: #304050; font-size: 11px; margin-top: 28px; border-top: 1px solid rgba(0,190,255,0.1); padding-top: 16px; }
    .warn { color: #ff8080; font-size: 12px; margin-top: 12px; }
  </style>
</head>
<body>
  <div class="container">
    <div class="title">ATLANTIS: DEEP SLUMBER</div>
    <div class="subtitle">IDENTITY VERIFICATION PROTOCOL</div>
    <div class="welcome-text">Chào mừng, <strong style="color:#00e8ff">${displayName}</strong>.</div>
    <div class="instruction-text">Nhập mã sau vào game để kích hoạt tài khoản của bạn:</div>
    <div class="otp-box">
      <div class="otp-code">${otp}</div>
      <div class="ttl">⏱ Mã có hiệu lực trong <strong>10 phút</strong></div>
    </div>
    <p class="warn">⚠ Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email này.</p>
    <div class="footer">
      Atlantis: Deep Slumber — Pre-Alpha 0.2<br>
      Email này được gửi tự động, vui lòng không trả lời.
    </div>
  </div>
</body>
</html>`
    };
    const info = await transporter.sendMail(mailOptions);
    console.log('[Email] Message sent: %s', info.messageId);
}

function validateEmail(email) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

// ─── Routes ───────────────────────────────────────────────────────────────────

// Health check
app.get('/health', (req, res) => {
    res.json({ status: 'ok', service: 'Atlantis Auth Backend' });
});

// Ping check endpoint (Đo độ trễ và kiểm tra độ ổn định kết nối cho Web & Game)
app.all(['/api/ping', '/ping'], (req, res) => {
    const clientTime = req.body?.clientTime || Number(req.query?.clientTime) || null;
    res.json({
        success: true,
        status: 'online',
        serverTime: Date.now(),
        clientTime: clientTime
    });
});

// POST /api/register
app.post('/api/register', async (req, res) => {
    try {
        const { displayName, email, password } = req.body;

        // Validation
        if (!displayName || !email || !password)
            return res.status(400).json({ success: false, message: 'Vui lòng điền đầy đủ thông tin.' });
        if (displayName.trim().length < 2 || displayName.trim().length > 32)
            return res.status(400).json({ success: false, message: 'Tên hiển thị phải từ 2-32 ký tự.' });
        if (!validateEmail(email))
            return res.status(400).json({ success: false, message: 'Địa chỉ email không hợp lệ.' });
        if (password.length < 8)
            return res.status(400).json({ success: false, message: 'Mật khẩu phải có ít nhất 8 ký tự.' });

        // Kiểm tra email đã tồn tại
        const existing = await User.findOne({ email: email.toLowerCase() });
        if (existing) {
            if (existing.isVerified)
                return res.status(409).json({ success: false, message: 'Email này đã được đăng ký. Vui lòng đăng nhập.' });
            // Tài khoản chưa xác nhận → cho phép gửi lại OTP
            const otp = generateOTP();
            existing.otp = otp;
            existing.otpExpiry = new Date(Date.now() + 10 * 60 * 1000);
            existing.otpAttempts = 0;
            await existing.save();
            await sendOtpEmail(email, existing.displayName, otp);
            return res.json({ success: true, message: 'Tài khoản chưa xác nhận. Mã OTP mới đã được gửi.', email });
        }

        // Hash password
        const passwordHash = await bcrypt.hash(password, 12);

        // Tạo OTP
        const otp = generateOTP();
        const otpExpiry = new Date(Date.now() + 10 * 60 * 1000); // 10 phút

        // Lưu user
        const user = new User({
            displayName: displayName.trim(),
            email: email.toLowerCase(),
            passwordHash,
            otp,
            otpExpiry,
            otpAttempts: 0
        });
        await user.save();

        // Gửi email
        await sendOtpEmail(email, displayName.trim(), otp);

        res.json({
            success: true,
            message: 'Đăng ký thành công! Mã xác nhận đã được gửi tới email của bạn.',
            email: email.toLowerCase()
        });
    } catch (err) {
        console.error('[Register]', err);
        res.status(500).json({ success: false, message: 'Lỗi máy chủ. Vui lòng thử lại.' });
    }
});

// POST /api/verify-otp
app.post('/api/verify-otp', async (req, res) => {
    try {
        const { email, otp } = req.body;
        if (!email || !otp)
            return res.status(400).json({ success: false, message: 'Thiếu thông tin xác nhận.' });

        const user = await User.findOne({ email: email.toLowerCase() });
        if (!user)
            return res.status(404).json({ success: false, message: 'Không tìm thấy tài khoản.' });
        if (user.isVerified)
            return res.json({ success: true, message: 'Tài khoản đã được xác nhận. Bạn có thể đăng nhập.' });

        // Kiểm tra OTP hết hạn
        if (!user.otp || !user.otpExpiry || new Date() > user.otpExpiry) {
            return res.status(400).json({ success: false, message: 'Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.' });
        }

        // Giới hạn số lần thử (5 lần)
        if (user.otpAttempts >= 5) {
            user.otp = null;
            user.otpExpiry = null;
            await user.save();
            return res.status(429).json({ success: false, message: 'Quá nhiều lần thử. Vui lòng yêu cầu mã OTP mới.' });
        }

        if (user.otp !== otp.trim()) {
            user.otpAttempts += 1;
            await user.save();
            const remaining = 5 - user.otpAttempts;
            return res.status(400).json({ success: false, message: `Mã OTP không đúng. Còn ${remaining} lần thử.` });
        }

        // Xác nhận thành công
        user.isVerified = true;
        user.otp = null;
        user.otpExpiry = null;
        user.otpAttempts = 0;
        await user.save();

        res.json({ success: true, message: 'Tài khoản đã được xác nhận! Bạn có thể đăng nhập.' });
    } catch (err) {
        console.error('[VerifyOTP]', err);
        res.status(500).json({ success: false, message: 'Lỗi máy chủ. Vui lòng thử lại.' });
    }
});

// POST /api/resend-otp
app.post('/api/resend-otp', async (req, res) => {
    try {
        const { email } = req.body;
        if (!email)
            return res.status(400).json({ success: false, message: 'Thiếu email.' });

        const user = await User.findOne({ email: email.toLowerCase() });
        if (!user)
            return res.status(404).json({ success: false, message: 'Không tìm thấy tài khoản.' });
        if (user.isVerified)
            return res.json({ success: true, message: 'Tài khoản đã được xác nhận.' });

        // Rate limit: cho phép gửi lại sau mỗi 30 giây (Mã hết hạn trong 10p, nên nếu còn > 9.5p là chưa cho gửi)
        const COOL_DOWN_MS = 30 * 1000;
        const timeElapsed = 10 * 60 * 1000 - (user.otpExpiry - Date.now());

        if (timeElapsed < COOL_DOWN_MS) {
            const remaining = Math.ceil((COOL_DOWN_MS - timeElapsed) / 1000);
            return res.status(429).json({
                success: false,
                message: `Vui lòng đợi ${remaining} giây để gửi lại mã.`
            });
        }

        const otp = generateOTP();
        user.otp = otp;
        user.otpExpiry = new Date(Date.now() + 10 * 60 * 1000);
        user.otpAttempts = 0;
        await user.save();

        await sendOtpEmail(email, user.displayName, otp);

        res.json({ success: true, message: 'Mã OTP mới đã được gửi tới email của bạn.' });
    } catch (err) {
        console.error('[ResendOTP]', err);
        res.status(500).json({ success: false, message: 'Lỗi máy chủ. Vui lòng thử lại.' });
    }
});

// POST /api/login
app.post('/api/login', async (req, res) => {
    try {
        const { email, password } = req.body;
        if (!email || !password)
            return res.status(400).json({ success: false, message: 'Vui lòng nhập email và mật khẩu.' });

        const user = await User.findOne({ email: email.toLowerCase() });
        if (!user)
            return res.status(401).json({ success: false, message: 'Email hoặc mật khẩu không đúng.' });
        if (!user.isVerified)
            return res.status(403).json({ success: false, message: 'Tài khoản chưa được xác nhận. Vui lòng kiểm tra email.', needsVerification: true, email: user.email });

        const passwordMatch = await bcrypt.compare(password, user.passwordHash);
        if (!passwordMatch)
            return res.status(401).json({ success: false, message: 'Email hoặc mật khẩu không đúng.' });

        // Tạo JWT token (7 ngày)
        const token = jwt.sign(
            { userId: user._id, email: user.email, displayName: user.displayName },
            process.env.JWT_SECRET,
            { expiresIn: '7d' }
        );

        res.json({
            success: true,
            message: 'Đăng nhập thành công!',
            token,
            displayName: user.displayName,
            email: user.email
        });
    } catch (err) {
        console.error('[Login]', err);
        res.status(500).json({ success: false, message: 'Lỗi máy chủ. Vui lòng thử lại.' });
    }
});

// ─── Room Routes ──────────────────────────────────────────────────────────────

function generateRoomId() {
    const chars = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ';
    let result = '';
    for (let i = 0; i < 6; i++) {
        result += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    return result;
}

// Middleware xác thực JWT
const authenticateToken = (req, res, next) => {
    const authHeader = req.headers['authorization'];
    const token = authHeader && authHeader.split(' ')[1];
    if (!token) return res.status(401).json({ success: false, message: 'Thiếu token xác thực.' });

    jwt.verify(token, process.env.JWT_SECRET, (err, user) => {
        if (err) return res.status(403).json({ success: false, message: 'Token không hợp lệ hoặc đã hết hạn.' });
        req.user = user;
        next();
    });
};

// POST /api/rooms/create
app.post('/api/rooms/create', authenticateToken, async (req, res) => {
    try {
        const { roomName, isPrivate, password } = req.body;
        if (!roomName) return res.status(400).json({ success: false, message: 'Thiếu tên phòng.' });

        const roomId = generateRoomId();
        const room = new Room({
            roomId,
            roomName,
            host: req.user.userId,
            players: [{
                user: req.user.userId,
                displayName: req.user.displayName,
                slot: 1
            }],
            isPrivate: !!isPrivate,
            password: isPrivate ? password : null
        });

        await room.save();
        res.json({ success: true, message: 'Tạo phòng thành công!', room });
    } catch (err) {
        console.error('[CreateRoom]', err);
        res.status(500).json({ success: false, message: `Lỗi khi tạo phòng: ${err.message}` });
    }
});

// POST /api/rooms/join
app.post('/api/rooms/join', authenticateToken, async (req, res) => {
    try {
        const { roomId, password } = req.body;
        const room = await Room.findOne({ roomId: roomId.toUpperCase(), status: 'waiting' });

        if (!room) return res.status(404).json({ success: false, message: 'Không tìm thấy phòng hoặc phòng đã bắt đầu.' });
        if (room.players.length >= room.maxPlayers) return res.status(400).json({ success: false, message: 'Phòng đã đầy.' });

        // Kiểm tra password nếu là phòng private
        if (room.isPrivate && room.password !== password) {
            return res.status(403).json({ success: false, message: 'Mật khẩu không đúng.' });
        }

        // Kiểm tra player đã trong phòng chưa
        if (room.players.find(p => p.user.toString() === req.user.userId)) {
            return res.json({ success: true, message: 'Bạn đã ở trong phòng này.', room });
        }

        // Tìm slot trống
        const occupiedSlots = room.players.map(p => p.slot);
        let assignedSlot = -1;
        for (let i = 1; i <= 4; i++) {
            if (!occupiedSlots.includes(i)) {
                assignedSlot = i;
                break;
            }
        }

        room.players.push({
            user: req.user.userId,
            displayName: req.user.displayName,
            slot: assignedSlot
        });

        await room.save();
        res.json({ success: true, message: 'Tham gia phòng thành công!', room });
    } catch (err) {
        console.error('[JoinRoom]', err);
        res.status(500).json({ success: false, message: `Lỗi khi tham gia phòng: ${err.message}` });
    }
});

// GET /api/rooms (Lấy danh sách tất cả các phòng đang chờ)
app.get('/api/rooms', async (req, res) => {
    try {
        // Chỉ lấy những phòng đang ở trạng thái 'waiting'
        // Đồng thời lấy thêm thông tin displayName của host từ bảng User
        const rooms = await Room.find({ status: 'waiting' })
            .populate('host', 'displayName')
            .sort({ createdAt: -1 });

        res.json({ success: true, rooms });
    } catch (err) {
        console.error('[GetRooms List]', err);
        res.status(500).json({ success: false, message: `Lỗi khi lấy danh sách phòng: ${err.message}` });
    }
});

// GET /api/rooms/:roomId
app.get('/api/rooms/:roomId', authenticateToken, async (req, res) => {

    try {
        const room = await Room.findOne({ roomId: req.params.roomId.toUpperCase() });
        if (!room) return res.status(404).json({ success: false, message: 'Không tìm thấy phòng.' });

        res.json({ success: true, room });
    } catch (err) {
        console.error('[GetRoom]', err);
        res.status(500).json({ success: false, message: `Lỗi khi lấy thông tin phòng: ${err.message}` });
    }
});


// POST /api/rooms/leave (Rời phòng & Nhượng quyền chủ phòng)
app.post('/api/rooms/leave', authenticateToken, async (req, res) => {
    try {
        const { roomId } = req.body;
        const room = await Room.findOne({ roomId: roomId.toUpperCase() });
        
        if (!room) return res.status(404).json({ success: false, message: 'Không tìm thấy phòng.' });

        // Xóa player khỏi danh sách
        const playerIndex = room.players.findIndex(p => p.user.toString() === req.user.userId);
        if (playerIndex > -1) {
            room.players.splice(playerIndex, 1);
        }

        // Nếu phòng không còn ai -> Xóa phòng
        if (room.players.length === 0) {
            await Room.deleteOne({ _id: room._id });
            return res.json({ success: true, message: 'Phòng trống, đã xóa phòng.' });
        }

        // Nếu người rời đi là Host -> Nhượng quyền cho người đầu tiên còn lại
        if (room.host.toString() === req.user.userId) {
            room.host = room.players[0].user;
            console.log(`[Room] Host migration: New host is ${room.players[0].displayName}`);
        }

        await room.save();
        res.json({ success: true, message: 'Đã rời phòng thành công.', room });
    } catch (err) {
        console.error('[LeaveRoom]', err);
        res.status(500).json({ success: false, message: `Lỗi khi rời phòng: ${err.message}` });
    }
});

// ─── Player State Routes (Network Sync & Save) ─────────────────────────────────

// GET /api/player/state (Lấy trạng thái nhân vật)
app.get('/api/player/state', authenticateToken, async (req, res) => {
    try {
        const user = await User.findById(req.user.userId);
        if (!user) return res.status(404).json({ success: false, message: 'Không tìm thấy người chơi.' });

        // Trả về playerState đã lưu, hoặc các giá trị mặc định
        const state = user.playerState || {
            health: 100,
            activeWeaponIndex: 1,
            isWeapon2Locked: true,
            isSkillsUnlocked: false,
            inventorySlots: ["", "", "", "", "", "", "", "", "", ""],
            upgradePoints: 5,
            hpLevel: 0,
            mpLevel: 0,
            cooldownLevel: 0,
            damageLevel: 0
        };

        res.json({ success: true, playerState: state });
    } catch (err) {
        console.error('[GetPlayerState]', err);
        res.status(500).json({ success: false, message: `Lỗi khi lấy trạng thái nhân vật: ${err.message}` });
    }
});

// POST /api/player/state (Lưu trạng thái nhân vật)
app.post('/api/player/state', authenticateToken, async (req, res) => {
    try {
        const { health, activeWeaponIndex, isWeapon2Locked, isSkillsUnlocked, inventorySlots, upgradePoints, hpLevel, mpLevel, cooldownLevel, damageLevel } = req.body;
        const user = await User.findById(req.user.userId);
        if (!user) return res.status(404).json({ success: false, message: 'Không tìm thấy người chơi.' });

        user.playerState = {
            health: health ?? 100,
            activeWeaponIndex: activeWeaponIndex ?? 1,
            isWeapon2Locked: isWeapon2Locked ?? true,
            isSkillsUnlocked: isSkillsUnlocked ?? false,
            inventorySlots: inventorySlots ?? ["", "", "", "", "", "", "", "", "", ""],
            upgradePoints: upgradePoints ?? 5,
            hpLevel: hpLevel ?? 0,
            mpLevel: mpLevel ?? 0,
            cooldownLevel: cooldownLevel ?? 0,
            damageLevel: damageLevel ?? 0
        };

        await user.save();
        res.json({ success: true, message: 'Lưu trạng thái nhân vật thành công!', playerState: user.playerState });
    } catch (err) {
        console.error('[SavePlayerState]', err);
        res.status(500).json({ success: false, message: `Lỗi khi lưu trạng thái nhân vật: ${err.message}` });
    }
});


// ─── Admin Management Routes ──────────────────────────────────────────────────

// Middleware xác thực Admin bằng JWT Token
const authenticateAdminToken = (req, res, next) => {
    const authHeader = req.headers['authorization'];
    const token = authHeader && authHeader.split(' ')[1];
    if (!token) return res.status(401).json({ success: false, message: 'Thiếu token xác thực quản trị.' });

    jwt.verify(token, process.env.JWT_SECRET, (err, admin) => {
        if (err) return res.status(403).json({ success: false, message: 'Phiên đăng nhập admin đã hết hạn hoặc không hợp lệ.' });
        req.admin = admin; // Chứa { adminId, username, displayName }
        next();
    });
};

// Middleware kiểm tra quyền Quản trị viên cấp cao (Super Admin)
const requireSuperAdmin = async (req, res, next) => {
    try {
        const admin = await Admin.findById(req.admin.adminId);
        if (admin && admin.role === 'superadmin') {
            next();
        } else {
            res.status(403).json({ success: false, message: 'Quyền truy cập bị từ chối. Chỉ dành cho quản trị viên cấp cao.' });
        }
    } catch (err) {
        res.status(500).json({ success: false, message: `Lỗi kiểm tra quyền: ${err.message}` });
    }
};

// Helper function để ghi log hoạt động admin
async function writeAdminLog(adminUsername, adminDisplayName, action, target, details = '') {
    try {
        const log = new AdminLog({
            adminUsername,
            adminDisplayName,
            action,
            target,
            details
        });
        await log.save();
    } catch (err) {
        console.error('[AdminLog Write Error]', err);
    }
}

// POST /api/admin/login (Đăng nhập quản trị viên)
app.post('/api/admin/login', async (req, res) => {
    try {
        const { username, password } = req.body;
        if (!username || !password) {
            return res.status(400).json({ success: false, message: 'Vui lòng nhập tài khoản và mật khẩu.' });
        }

        const admin = await Admin.findOne({ username: username.toLowerCase().trim() });
        if (!admin) {
            return res.status(401).json({ success: false, message: 'Tài khoản hoặc mật khẩu admin không đúng.' });
        }

        const isMatch = await bcrypt.compare(password, admin.passwordHash);
        if (!isMatch) {
            return res.status(401).json({ success: false, message: 'Tài khoản hoặc mật khẩu admin không đúng.' });
        }

        // Tạo token JWT có thời hạn 7 ngày
        const token = jwt.sign(
            { adminId: admin._id, username: admin.username, displayName: admin.displayName, role: admin.role },
            process.env.JWT_SECRET,
            { expiresIn: '7d' }
        );

        res.json({
            success: true,
            message: 'Đăng nhập quản trị thành công!',
            token,
            displayName: admin.displayName,
            username: admin.username,
            role: admin.role,
            isFirstLogin: admin.isFirstLogin
        });
    } catch (err) {
        console.error('[AdminLogin]', err);
        res.status(500).json({ success: false, message: `Lỗi máy chủ: ${err.message}` });
    }
});

// POST /api/admin/first-login-action (Xử lý thay đổi lần đầu: giữ hoặc đổi mật khẩu)
app.post('/api/admin/first-login-action', authenticateAdminToken, async (req, res) => {
    try {
        const { actionType, newPassword } = req.body;
        const admin = await Admin.findById(req.admin.adminId);
        if (!admin) return res.status(404).json({ success: false, message: 'Không tìm thấy quản trị viên.' });

        if (actionType === 'change') {
            if (!newPassword || newPassword.length < 6) {
                return res.status(400).json({ success: false, message: 'Mật khẩu mới phải có ít nhất 6 ký tự.' });
            }
            admin.passwordHash = await bcrypt.hash(newPassword, 12);
            await writeAdminLog(admin.username, admin.displayName, 'Đổi mật khẩu', 'Hệ thống', 'Đổi mật khẩu mặc định thành mật khẩu mới trong lần đăng nhập đầu tiên.');
        } else {
            await writeAdminLog(admin.username, admin.displayName, 'Giữ mật khẩu', 'Hệ thống', 'Giữ mật khẩu mặc định trong lần đăng nhập đầu tiên.');
        }

        admin.isFirstLogin = false;
        await admin.save();

        res.json({ success: true, message: 'Cập nhật trạng thái đăng nhập đầu tiên thành công!' });
    } catch (err) {
        console.error('[AdminFirstLoginAction]', err);
        res.status(500).json({ success: false, message: `Lỗi máy chủ: ${err.message}` });
    }
});

// POST /api/admin/change-password (Đổi mật khẩu quản trị viên chủ động)
app.post('/api/admin/change-password', authenticateAdminToken, async (req, res) => {
    try {
        const { oldPassword, newPassword } = req.body;
        if (!oldPassword || !newPassword) {
            return res.status(400).json({ success: false, message: 'Vui lòng điền đầy đủ mật khẩu cũ và mới.' });
        }
        if (newPassword.length < 6) {
            return res.status(400).json({ success: false, message: 'Mật khẩu mới phải có ít nhất 6 ký tự.' });
        }

        const admin = await Admin.findById(req.admin.adminId);
        if (!admin) return res.status(404).json({ success: false, message: 'Không tìm thấy quản trị viên.' });

        const isMatch = await bcrypt.compare(oldPassword, admin.passwordHash);
        if (!isMatch) {
            return res.status(400).json({ success: false, message: 'Mật khẩu cũ không chính xác.' });
        }

        admin.passwordHash = await bcrypt.hash(newPassword, 12);
        admin.isFirstLogin = false;
        await admin.save();

        await writeAdminLog(admin.username, admin.displayName, 'Đổi mật khẩu', 'Hệ thống', 'Đổi mật khẩu quản trị viên thành công.');

        res.json({ success: true, message: 'Đổi mật khẩu thành công!' });
    } catch (err) {
        console.error('[AdminChangePassword]', err);
        res.status(500).json({ success: false, message: `Lỗi máy chủ: ${err.message}` });
    }
});

// GET /api/admin/logs (Lấy lịch sử logs hoạt động)
app.get('/api/admin/logs', authenticateAdminToken, async (req, res) => {
    try {
        const logs = await AdminLog.find().sort({ createdAt: -1 }).limit(100);
        res.json({ success: true, logs });
    } catch (err) {
        console.error('[AdminGetLogs]', err);
        res.status(500).json({ success: false, message: `Lỗi tải nhật ký hoạt động: ${err.message}` });
    }
});

// GET /api/admin/accounts (Liệt kê danh sách tất cả các tài khoản admin - Chỉ dành cho Super Admin)
app.get('/api/admin/accounts', authenticateAdminToken, requireSuperAdmin, async (req, res) => {
    try {
        const admins = await Admin.find({}, '-passwordHash').sort({ createdAt: 1 });
        res.json({ success: true, admins });
    } catch (err) {
        console.error('[AdminGetAccounts]', err);
        res.status(500).json({ success: false, message: `Lỗi tải danh sách admin: ${err.message}` });
    }
});

// POST /api/admin/accounts/:adminId/reset-password (Đặt lại mật khẩu của một admin về 123456 - Chỉ dành cho Super Admin)
app.post('/api/admin/accounts/:adminId/reset-password', authenticateAdminToken, requireSuperAdmin, async (req, res) => {
    try {
        const adminToReset = await Admin.findById(req.params.adminId);
        if (!adminToReset) {
            return res.status(404).json({ success: false, message: 'Không tìm thấy tài khoản quản trị cần đặt lại.' });
        }

        if (adminToReset._id.toString() === req.admin.adminId) {
            return res.status(400).json({ success: false, message: 'Không thể tự đặt lại mật khẩu bằng chức năng này. Vui lòng dùng chức năng Đổi mật khẩu của cá nhân.' });
        }

        const defaultPasswordHash = await bcrypt.hash('123456', 12);
        adminToReset.passwordHash = defaultPasswordHash;
        adminToReset.isFirstLogin = true; // Yêu cầu đổi mật khẩu lại lần đầu
        await adminToReset.save();

        // Ghi Log Hoạt Động
        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            'Reset mật khẩu',
            `Quản trị viên: ${adminToReset.displayName} (${adminToReset.username})`,
            'Đặt lại mật khẩu quản trị viên về mặc định (123456) và yêu cầu đổi mật khẩu ở lần đăng nhập tiếp theo.'
        );

        res.json({ success: true, message: `Đã đặt lại mật khẩu cho quản trị viên ${adminToReset.displayName} thành công!` });
    } catch (err) {
        console.error('[AdminResetPassword]', err);
        res.status(500).json({ success: false, message: `Lỗi khi đặt lại mật khẩu: ${err.message}` });
    }
});

// GET /api/admin/stats (Lấy thống kê tổng quan)
app.get('/api/admin/stats', authenticateAdminToken, async (req, res) => {
    try {
        const totalUsers = await User.countDocuments();
        const verifiedUsers = await User.countDocuments({ isVerified: true });
        const totalRooms = await Room.countDocuments();
        const activeRooms = await Room.countDocuments({ status: { $in: ['waiting', 'playing'] } });

        const userStats = await User.aggregate([
            {
                $group: {
                    _id: null,
                    avgHpLevel: { $avg: '$playerState.hpLevel' },
                    avgDamageLevel: { $avg: '$playerState.damageLevel' },
                    avgUpgradePoints: { $avg: '$playerState.upgradePoints' }
                }
            }
        ]);

        const stats = {
            totalUsers,
            verifiedUsers,
            unverifiedUsers: totalUsers - verifiedUsers,
            totalRooms,
            activeRooms,
            avgHpLevel: userStats[0] ? Math.round(userStats[0].avgHpLevel * 10) / 10 : 0,
            avgDamageLevel: userStats[0] ? Math.round(userStats[0].avgDamageLevel * 10) / 10 : 0,
            avgUpgradePoints: userStats[0] ? Math.round(userStats[0].avgUpgradePoints * 10) / 10 : 0
        };

        res.json({ success: true, stats });
    } catch (err) {
        console.error('[AdminStats]', err);
        res.status(500).json({ success: false, message: `Lỗi lấy thống kê: ${err.message}` });
    }
});

// GET /api/admin/users (Lấy danh sách người dùng)
app.get('/api/admin/users', authenticateAdminToken, async (req, res) => {
    try {
        const users = await User.find({}, '-passwordHash').sort({ createdAt: -1 });
        res.json({ success: true, users });
    } catch (err) {
        console.error('[AdminGetUsers]', err);
        res.status(500).json({ success: false, message: `Lỗi lấy danh sách người chơi: ${err.message}` });
    }
});

// POST /api/admin/users/:userId (Cập nhật thông tin và playerState của người dùng)
app.post('/api/admin/users/:userId', authenticateAdminToken, async (req, res) => {
    try {
        const { displayName, email, isVerified, playerState } = req.body;
        const user = await User.findById(req.params.userId);
        if (!user) return res.status(404).json({ success: false, message: 'Không tìm thấy người chơi.' });

        const oldName = user.displayName;
        const oldEmail = user.email;

        if (displayName) user.displayName = displayName.trim();
        if (email) user.email = email.toLowerCase().trim();
        if (isVerified !== undefined) user.isVerified = !!isVerified;

        if (playerState) {
            user.playerState = {
                health: playerState.health !== undefined ? playerState.health : user.playerState.health,
                activeWeaponIndex: playerState.activeWeaponIndex !== undefined ? playerState.activeWeaponIndex : user.playerState.activeWeaponIndex,
                isWeapon2Locked: playerState.isWeapon2Locked !== undefined ? playerState.isWeapon2Locked : user.playerState.isWeapon2Locked,
                isSkillsUnlocked: playerState.isSkillsUnlocked !== undefined ? playerState.isSkillsUnlocked : user.playerState.isSkillsUnlocked,
                inventorySlots: playerState.inventorySlots || user.playerState.inventorySlots,
                upgradePoints: playerState.upgradePoints !== undefined ? playerState.upgradePoints : user.playerState.upgradePoints,
                hpLevel: playerState.hpLevel !== undefined ? playerState.hpLevel : user.playerState.hpLevel,
                mpLevel: playerState.mpLevel !== undefined ? playerState.mpLevel : user.playerState.mpLevel,
                cooldownLevel: playerState.cooldownLevel !== undefined ? playerState.cooldownLevel : user.playerState.cooldownLevel,
                damageLevel: playerState.damageLevel !== undefined ? playerState.damageLevel : user.playerState.damageLevel
            };
        }

        await user.save();
        const updatedUser = user.toObject();
        delete updatedUser.passwordHash;

        // Ghi Log Hoạt Động
        let logDetails = `Thay đổi: `;
        if (displayName && oldName !== displayName) logDetails += `Tên (${oldName} -> ${displayName}). `;
        if (email && oldEmail !== email) logDetails += `Email (${oldEmail} -> ${email}). `;
        if (playerState) logDetails += `Đã cập nhật Trạng thái nhân vật (Máu, Cấp độ, Điểm nâng cấp). `;

        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            'Sửa chỉ số',
            `Người chơi: ${user.displayName} (${user._id})`,
            logDetails || 'Không có thay đổi thông tin cơ bản.'
        );

        res.json({ success: true, message: 'Cập nhật người chơi thành công!', user: updatedUser });
    } catch (err) {
        console.error('[AdminUpdateUser]', err);
        res.status(500).json({ success: false, message: `Lỗi cập nhật người chơi: ${err.message}` });
    }
});

// DELETE /api/admin/users/:userId (Xóa người dùng và dọn dẹp các dữ liệu phòng liên quan)
app.delete('/api/admin/users/:userId', authenticateAdminToken, async (req, res) => {
    try {
        const user = await User.findById(req.params.userId);
        if (!user) return res.status(404).json({ success: false, message: 'Không tìm thấy người chơi.' });

        const targetName = user.displayName;

        // Xóa các phòng do user này làm host
        await Room.deleteMany({ host: req.params.userId });
        
        // Xóa user khỏi danh sách players trong các phòng khác
        await Room.updateMany(
            { "players.user": req.params.userId },
            { $pull: { players: { user: req.params.userId } } }
        );

        // Xóa user
        await User.deleteOne({ _id: req.params.userId });

        // Ghi Log Hoạt Động
        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            'Xóa tài khoản',
            `Người chơi: ${targetName} (${req.params.userId})`,
            'Đã xóa vĩnh viễn tài khoản người chơi khỏi hệ thống và dọn dẹp các phòng chơi liên quan.'
        );

        res.json({ success: true, message: 'Đã xóa người chơi và dọn dẹp dữ liệu phòng liên quan.' });
    } catch (err) {
        console.error('[AdminDeleteUser]', err);
        res.status(500).json({ success: false, message: `Lỗi khi xóa người chơi: ${err.message}` });
    }
});

// GET /api/admin/rooms (Lấy danh sách tất cả các phòng)
app.get('/api/admin/rooms', authenticateAdminToken, async (req, res) => {
    try {
        const rooms = await Room.find()
            .populate('host', 'displayName email')
            .sort({ createdAt: -1 });
        res.json({ success: true, rooms });
    } catch (err) {
        console.error('[AdminGetRooms]', err);
        res.status(500).json({ success: false, message: `Lỗi lấy danh sách phòng: ${err.message}` });
    }
});

// DELETE /api/admin/rooms/:roomId (Xóa/Đóng phòng chơi)
app.delete('/api/admin/rooms/:roomId', authenticateAdminToken, async (req, res) => {
    try {
        const room = await Room.findOne({ roomId: req.params.roomId.toUpperCase() });
        if (!room) return res.status(404).json({ success: false, message: 'Không tìm thấy phòng chơi.' });

        await Room.deleteOne({ _id: room._id });

        // Ghi Log Hoạt Động
        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            'Giải tán phòng',
            `Phòng chơi: ${room.roomName} (${req.params.roomId.toUpperCase()})`,
            'Cưỡng chế giải tán phòng chơi đang hoạt động hoặc đang chờ.'
        );

        res.json({ success: true, message: 'Đã xóa phòng chơi thành công.' });
    } catch (err) {
        console.error('[AdminDeleteRoom]', err);
        res.status(500).json({ success: false, message: `Lỗi khi xóa phòng chơi: ${err.message}` });
    }
});

// GET /api/admin/system-metrics (Lấy thông số CPU, RAM, Network, Database & Uptime)
app.get('/api/admin/system-metrics', authenticateAdminToken, async (req, res) => {
    try {
        const cpu = calculateCpuUsage();
        const loadAvg = os.loadavg(); // [1m, 5m, 15m]

        const totalMem = os.totalmem();
        const freeMem = os.freemem();
        const usedMem = totalMem - freeMem;
        const memUsagePercent = Math.round((usedMem / totalMem) * 1000) / 10;
        const processMemory = process.memoryUsage();

        // Network Interfaces
        const rawInterfaces = os.networkInterfaces();
        const interfaces = [];
        for (const [name, addrs] of Object.entries(rawInterfaces)) {
            if (!addrs) continue;
            for (const addr of addrs) {
                if (addr.family === 'IPv4') {
                    interfaces.push({
                        name,
                        address: addr.address,
                        netmask: addr.netmask,
                        mac: addr.mac,
                        internal: addr.internal
                    });
                }
            }
        }

        // Tính RPS (Requests Per Second) trong 10 giây qua
        const now = Date.now();
        const recentTenSecCount = networkStats.recentRequestTimestamps.filter(t => t >= now - 10000).length;
        const requestsPerSec = Math.round((recentTenSecCount / 10) * 10) / 10;

        // MongoDB Ping Latency
        let dbPingMs = 0;
        let dbState = 'disconnected';
        if (mongoose.connection.readyState === 1) {
            dbState = 'connected';
            const startPing = Date.now();
            try {
                await mongoose.connection.db.admin().ping();
                dbPingMs = Date.now() - startPing;
            } catch (e) {
                dbPingMs = -1;
            }
        } else if (mongoose.connection.readyState === 2) {
            dbState = 'connecting';
        }

        const metrics = {
            cpu: {
                usagePercent: cpu.usagePercent,
                cores: cpu.cores,
                model: cpu.model,
                speed: cpu.speed,
                loadAvg: loadAvg.map(l => Math.round(l * 100) / 100)
            },
            memory: {
                totalBytes: totalMem,
                usedBytes: usedMem,
                freeBytes: freeMem,
                usagePercent: memUsagePercent,
                totalMB: Math.round(totalMem / (1024 * 1024)),
                usedMB: Math.round(usedMem / (1024 * 1024)),
                freeMB: Math.round(freeMem / (1024 * 1024)),
                process: {
                    rssMB: Math.round(processMemory.rss / (1024 * 1024) * 10) / 10,
                    heapTotalMB: Math.round(processMemory.heapTotal / (1024 * 1024) * 10) / 10,
                    heapUsedMB: Math.round(processMemory.heapUsed / (1024 * 1024) * 10) / 10,
                    externalMB: Math.round(processMemory.external / (1024 * 1024) * 10) / 10
                }
            },
            network: {
                totalRequests: networkStats.totalRequests,
                totalBytesRx: networkStats.totalBytesRx,
                totalBytesTx: networkStats.totalBytesTx,
                activeConnections: networkStats.activeConnections,
                requestsPerSec,
                interfaces
            },
            database: {
                status: dbState,
                pingLatencyMs: dbPingMs
            },
            system: {
                hostname: os.hostname(),
                platform: os.platform(),
                arch: os.arch(),
                release: os.release(),
                nodeVersion: process.version,
                osUptimeSec: Math.floor(os.uptime()),
                processUptimeSec: Math.floor(process.uptime()),
                timestamp: new Date().toISOString()
            }
        };

        res.json({ success: true, metrics });
    } catch (err) {
        console.error('[AdminSystemMetrics]', err);
        res.status(500).json({ success: false, message: `Lỗi thu thập thông số hệ thống: ${err.message}` });
    }
});

// POST /api/admin/server/restart (Khởi động lại Server an toàn)
app.post('/api/admin/server/restart', authenticateAdminToken, async (req, res) => {
    try {
        const reason = req.body?.reason || 'Quản trị viên yêu cầu khởi động lại qua Control Panel';

        // Ghi Log Hoạt Động
        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            'Khởi động lại Server',
            'Hệ thống Máy chủ Atlantis',
            `Khởi động lại tiến trình server. Lý do: ${reason}`
        );

        res.json({
            success: true,
            message: 'Lệnh khởi động lại đã được tiếp nhận. Máy chủ sẽ tự khởi động lại trong 1 giây...'
        });

        // Hẹn giờ khởi động lại để đảm bảo client nhận được response
        setTimeout(() => {
            console.log(`\x1b[33m%s\x1b[0m`, `[Server] Restart triggered by admin: ${req.admin.username} (${req.admin.displayName})`);
            process.exit(0);
        }, 1000);
    } catch (err) {
        console.error('[AdminRestartServer]', err);
        res.status(500).json({ success: false, message: `Lỗi khi yêu cầu khởi động lại: ${err.message}` });
    }
});

// POST /api/admin/rooms/cleanup (Dọn dẹp các phòng rác / không có người chơi)
app.post('/api/admin/rooms/cleanup', authenticateAdminToken, async (req, res) => {
    try {
        const result = await Room.deleteMany({
            $or: [
                { players: { $size: 0 } },
                { status: 'ended' }
            ]
        });

        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            'Dọn dẹp phòng chơi',
            'Cơ sở dữ liệu Phòng',
            `Đã dọn dẹp ${result.deletedCount} phòng chơi trống hoặc đã kết thúc.`
        );

        res.json({
            success: true,
            message: `Đã dọn dẹp thành công ${result.deletedCount} phòng rác!`,
            deletedCount: result.deletedCount
        });
    } catch (err) {
        console.error('[AdminCleanupRooms]', err);
        res.status(500).json({ success: false, message: `Lỗi dọn dẹp phòng: ${err.message}` });
    }
});

// ─── DOCKER GAME SERVER MANAGER & DEPLOYMENT ENGINE ──────────────────────────

const dockerDeployState = {
    isDeploying: false,
    currentStep: 0,
    totalSteps: 4,
    stepName: '',
    status: 'idle', // 'idle' | 'running' | 'success' | 'error'
    logs: [],
    startTime: null,
    endTime: null,
    error: null
};

function appendDeployLog(text) {
    if (!text) return;
    const lines = text.toString().split('\n');
    for (const line of lines) {
        if (line.trim().length > 0) {
            const time = new Date().toLocaleTimeString('vi-VN');
            dockerDeployState.logs.push(`[${time}] ${line}`);
            if (dockerDeployState.logs.length > 1500) {
                dockerDeployState.logs.shift();
            }
        }
    }
}

function runExecCommand(cmd, options = {}) {
    return new Promise((resolve) => {
        appendDeployLog(`$ ${cmd}`);
        let stdoutAcc = '';
        let stderrAcc = '';

        const child = spawn(cmd, { shell: true, ...options });

        child.stdout.on('data', (chunk) => {
            const str = chunk.toString();
            stdoutAcc += str;
            appendDeployLog(str);
        });

        child.stderr.on('data', (chunk) => {
            const str = chunk.toString();
            stderrAcc += str;
            appendDeployLog(str);
        });

        child.on('close', (code) => {
            resolve({ code: code || 0, stdout: stdoutAcc, stderr: stderrAcc });
        });

        child.on('error', (err) => {
            appendDeployLog(`[Lỗi thực thi]: ${err.message}`);
            resolve({ code: 1, error: err, stdout: stdoutAcc, stderr: stderrAcc });
        });
    });
}

function getGameServerDir() {
    const candidateDirs = [
        '/root/AtlantisDeepSlumberServer',
        path.resolve(__dirname, '..'),
        '/root',
        process.cwd()
    ];
    for (const d of candidateDirs) {
        try {
            if (fs.existsSync(d) && (fs.existsSync(path.join(d, 'Dockerfile')) || fs.existsSync(path.join(d, 'Assets')))) {
                return d;
            }
        } catch (e) {}
    }
    return '/root/AtlantisDeepSlumberServer';
}

async function executeDockerDeploy(adminUser) {
    dockerDeployState.isDeploying = true;
    dockerDeployState.status = 'running';
    dockerDeployState.currentStep = 1;
    dockerDeployState.stepName = 'Dừng và xóa Container cũ (live_server)';
    dockerDeployState.logs = [];
    dockerDeployState.startTime = Date.now();
    dockerDeployState.endTime = null;
    dockerDeployState.error = null;

    appendDeployLog('══════════════════════════════════════════════════════════════════');
    appendDeployLog(`🚀 BẮT ĐẦU QUY TRÌNH REBUILD & THAY IMAGE DOCKER`);
    appendDeployLog(`👤 Người thực hiện: ${adminUser.displayName} (@${adminUser.username})`);
    appendDeployLog('══════════════════════════════════════════════════════════════════');

    try {
        // BƯỚC 1: docker stop live_server && docker rm live_server
        appendDeployLog('\n▶ [BƯỚC 1/4] Dừng và xóa Container cũ: live_server...');
        await runExecCommand('docker stop live_server 2>/dev/null || true');
        await runExecCommand('docker rm -f live_server 2>/dev/null || true');
        appendDeployLog('✔ [BƯỚC 1/4] Hoàn tất dừng & xóa container cũ.');

        // BƯỚC 2: docker rmi -f vps_server
        dockerDeployState.currentStep = 2;
        dockerDeployState.stepName = 'Xóa Image Docker cũ (vps_server)';
        appendDeployLog('\n▶ [BƯỚC 2/4] Xóa Image cũ để giải phóng dung lượng đĩa...');
        await runExecCommand('docker rmi -f vps_server 2>/dev/null || true');
        appendDeployLog('✔ [BƯỚC 2/4] Đã xóa image cũ.');

        // BƯỚC 3: docker build -t vps_server .
        dockerDeployState.currentStep = 3;
        dockerDeployState.stepName = 'Build Image Docker mới (docker build -t vps_server .)';
        const workDir = getGameServerDir();
        appendDeployLog(`\n▶ [BƯỚC 3/4] Đang Build Docker Image mới tại thư mục: ${workDir}...`);
        
        const buildResult = await runExecCommand('docker build -t vps_server .', { cwd: workDir });
        if (buildResult.code !== 0 && !buildResult.stdout?.includes('Successfully tagged') && !buildResult.stdout?.includes('naming to docker.io/library/vps_server')) {
            throw new Error(`Lỗi khi Build Docker Image: ${buildResult.stderr || buildResult.stdout || 'Build failed'}`);
        }
        appendDeployLog('✔ [BƯỚC 3/4] Build Image Docker vps_server thành công!');

        // BƯỚC 4: docker run -d -p 7777:7777/udp --name live_server vps_server
        dockerDeployState.currentStep = 4;
        dockerDeployState.stepName = 'Khởi động Container mới (docker run -d -p 7777:7777/udp --name live_server vps_server)';
        appendDeployLog('\n▶ [BƯỚC 4/4] Khởi động Container mới (live_server) tại cổng 7777/udp...');
        
        const runResult = await runExecCommand('docker run -d -p 7777:7777/udp --name live_server vps_server');
        if (runResult.code !== 0) {
            throw new Error(`Lỗi khi khởi động Container: ${runResult.stderr || 'Run container failed'}`);
        }
        appendDeployLog('✔ [BƯỚC 4/4] Container live_server đã được khởi động thành công!');

        dockerDeployState.status = 'success';
        dockerDeployState.stepName = 'Hoàn tất Rebuild & Deploy!';
        dockerDeployState.endTime = Date.now();
        const duration = Math.round((dockerDeployState.endTime - dockerDeployState.startTime) / 1000);
        appendDeployLog(`\n🎉 HOÀN TẤT THÀNH CÔNG trong ${duration} giây! Game Server đang online tại cổng 7777/udp.`);

        await writeAdminLog(
            adminUser.username,
            adminUser.displayName,
            'Rebuild Docker Game Server',
            'Docker Manager',
            `Đã hoàn tất Rebuild Image vps_server và khởi chạy container live_server trong ${duration}s.`
        );

    } catch (err) {
        dockerDeployState.status = 'error';
        dockerDeployState.error = err.message;
        dockerDeployState.endTime = Date.now();
        appendDeployLog(`\n❌ [LỖI TRIỂN KHAI]: ${err.message}`);
        console.error('[DockerDeploy Error]', err);
    } finally {
        dockerDeployState.isDeploying = false;
    }
}

// GET /api/admin/docker/status (Lấy trạng thái container live_server)
app.get('/api/admin/docker/status', authenticateAdminToken, (req, res) => {
    exec('docker inspect live_server', { timeout: 4000 }, (err, stdout) => {
        let containerInfo = {
            name: 'live_server',
            image: 'vps_server',
            status: 'not_found', // 'running' | 'exited' | 'not_found'
            state: 'Không tìm thấy container',
            ports: '7777:7777/udp',
            created: '-',
            startedAt: '-',
            raw: null
        };

        if (!err && stdout) {
            try {
                const parsed = JSON.parse(stdout);
                if (Array.isArray(parsed) && parsed.length > 0) {
                    const data = parsed[0];
                    const isRunning = data.State?.Running || false;
                    containerInfo = {
                        name: data.Name ? data.Name.replace(/^\//, '') : 'live_server',
                        image: data.Config?.Image || 'vps_server',
                        status: isRunning ? 'running' : 'exited',
                        state: isRunning ? 'Đang hoạt động (Running)' : `Đã dừng (${data.State?.Status || 'Exited'})`,
                        ports: '7777:7777/udp',
                        created: data.Created ? new Date(data.Created).toLocaleString('vi-VN') : '-',
                        startedAt: data.State?.StartedAt ? new Date(data.State.StartedAt).toLocaleString('vi-VN') : '-',
                        id: data.Id ? data.Id.substring(0, 12) : '-'
                    };
                }
            } catch (e) {}
        }

        res.json({
            success: true,
            container: containerInfo,
            deployState: {
                isDeploying: dockerDeployState.isDeploying,
                currentStep: dockerDeployState.currentStep,
                totalSteps: dockerDeployState.totalSteps,
                stepName: dockerDeployState.stepName,
                status: dockerDeployState.status,
                error: dockerDeployState.error
            }
        });
    });
});

// POST /api/admin/docker/deploy (Bắt đầu tiến trình Rebuild 4 bước)
app.post('/api/admin/docker/deploy', authenticateAdminToken, (req, res) => {
    if (dockerDeployState.isDeploying) {
        return res.status(400).json({
            success: false,
            message: 'Tiến trình Rebuild & Deploy Docker đang chạy. Vui lòng đợi hoàn tất.'
        });
    }

    // Chạy bất đồng bộ
    executeDockerDeploy(req.admin);

    res.json({
        success: true,
        message: 'Đã bắt đầu tiến trình Rebuild & Deploy Docker Image!'
    });
});

// GET /api/admin/docker/deploy-logs (Lấy log tiến trình deploy)
app.get('/api/admin/docker/deploy-logs', authenticateAdminToken, (req, res) => {
    res.json({
        success: true,
        deployState: dockerDeployState
    });
});

// GET /api/admin/docker/game-logs (Lấy log in-game từ container live_server)
app.get('/api/admin/docker/game-logs', authenticateAdminToken, (req, res) => {
    const tailLines = Number(req.query.lines) || 250;
    exec(`docker logs --tail ${tailLines} --timestamps live_server`, { maxBuffer: 1024 * 1024 * 5, timeout: 5000 }, (err, stdout, stderr) => {
        let logs = (stdout || '') + (stderr || '');
        if (err && !logs) {
            logs = `[Docker Logs]: Không thể lấy log container live_server (${err.message}). Có thể container chưa chạy.`;
        }
        res.json({
            success: true,
            logs: logs.trim() || 'Chưa có log từ Game Server.'
        });
    });
});

async function executeDockerDeployWithFile(adminUser, tempFilePath, stagingDir, filename, sizeMB, ext) {
    dockerDeployState.isDeploying = true;
    dockerDeployState.status = 'running';
    dockerDeployState.currentStep = 1;
    dockerDeployState.stepName = 'Giải nén & Merge file build vào máy chủ';
    dockerDeployState.logs = [];
    dockerDeployState.startTime = Date.now();
    dockerDeployState.endTime = null;
    dockerDeployState.error = null;

    appendDeployLog('══════════════════════════════════════════════════════════════════');
    appendDeployLog(`🚀 BẮT ĐẦU QUY TRÌNH DEPLOY BẢN BUILD MỚI`);
    appendDeployLog(`👤 Người thực hiện: ${adminUser.displayName} (@${adminUser.username})`);
    appendDeployLog(`📦 File build: ${filename} (${sizeMB} MB, định dạng .${ext})`);
    appendDeployLog('══════════════════════════════════════════════════════════════════');

    const workDir = getGameServerDir();

    try {
        // BƯỚC 1: GIẢI NÉN VÀ MERGE
        appendDeployLog('\n▶ [BƯỚC 1/4] Đang giải nén file build trên VPS...');
        
        try {
            if (!fs.existsSync(stagingDir)) fs.mkdirSync(stagingDir, { recursive: true });
        } catch (e) {}

        let extractCmd = '';
        if (ext === 'rar') {
            extractCmd = `(which unar >/dev/null 2>&1 || which 7z >/dev/null 2>&1 || (DEBIAN_FRONTEND=noninteractive apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq unar p7zip-full unzip)); unar -f -o "${stagingDir}" "${tempFilePath}" || 7z x -y "${tempFilePath}" -o"${stagingDir}" || unrar x -o+ "${tempFilePath}" "${stagingDir}/"`;
        } else if (ext === '7z') {
            extractCmd = `which 7z >/dev/null 2>&1 || (DEBIAN_FRONTEND=noninteractive apt-get update -qq && DEBIAN_FRONTEND=noninteractive apt-get install -y -qq p7zip-full); 7z x -y "${tempFilePath}" -o"${stagingDir}"`;
        } else if (ext === 'tar' || ext === 'gz' || ext === 'tgz') {
            extractCmd = `tar -xf "${tempFilePath}" -C "${stagingDir}"`;
        } else {
            extractCmd = `unzip -o "${tempFilePath}" -d "${stagingDir}" 2>/dev/null || (which 7z >/dev/null 2>&1 && 7z x -y "${tempFilePath}" -o"${stagingDir}") || python3 -m zipfile -e "${tempFilePath}" "${stagingDir}"`;
        }

        const extractRes = await runExecCommand(extractCmd, { maxBuffer: 1024 * 1024 * 20, timeout: 5 * 60 * 1000 });
        if (extractRes.code !== 0 && (!fs.existsSync(stagingDir) || fs.readdirSync(stagingDir).length === 0)) {
            throw new Error(`Lỗi giải nén file .${ext}: ${extractRes.stderr || 'Không thể giải nén file'}`);
        }
        appendDeployLog('✔ Giải nén thành công vào thư mục đệm!');

        appendDeployLog(`\n▶ [MERGE] Đang MERGE các file cập nhật vào thư mục máy chủ (${workDir})...`);
        let sourceMergePath = stagingDir;
        const innerCandidate = path.join(stagingDir, 'AtlantisDeepSlumberServer');
        if (fs.existsSync(innerCandidate) && fs.statSync(innerCandidate).isDirectory()) {
            sourceMergePath = innerCandidate;
            appendDeployLog(`ℹ Phát hiện thư mục lồng AtlantisDeepSlumberServer -> Merge toàn bộ nội dung con.`);
        }

        const mergeCmd = `cp -rf "${sourceMergePath}"/. "${workDir}/"`;
        const mergeRes = await runExecCommand(mergeCmd, { timeout: 60000 });
        if (mergeRes.code !== 0) {
            throw new Error(`Lỗi khi merge file vào thư mục server: ${mergeRes.stderr || 'Merge failed'}`);
        }
        appendDeployLog('✔ MERGE thư mục hoàn tất! Đã đồng bộ mã nguồn và giữ nguyên toàn bộ cấu hình hệ thống.');

        // Dọn dẹp file tạm
        try { fs.unlinkSync(tempFilePath); } catch (e) {}
        try { fs.rmSync(stagingDir, { recursive: true, force: true }); } catch (e) {}

        // BƯỚC 2: DỪNG & XÓA CONTAINER CŨ + XÓA IMAGE CŨ
        dockerDeployState.currentStep = 2;
        dockerDeployState.stepName = 'Dừng Container cũ & Xóa Image cũ';
        appendDeployLog('\n▶ [BƯỚC 2/4] Dừng container cũ và dọn dẹp image cũ...');
        await runExecCommand('docker stop live_server 2>/dev/null || true');
        await runExecCommand('docker rm -f live_server 2>/dev/null || true');
        await runExecCommand('docker rmi -f vps_server 2>/dev/null || true');
        appendDeployLog('✔ Đã dừng container cũ và xóa image cũ.');

        // BƯỚC 3: DOCKER BUILD
        dockerDeployState.currentStep = 3;
        dockerDeployState.stepName = 'Build Docker Image mới (vps_server)';
        appendDeployLog(`\n▶ [BƯỚC 3/4] Đang Build Docker Image mới tại thư mục: ${workDir}...`);
        const buildResult = await runExecCommand('docker build -t vps_server .', { cwd: workDir });
        if (buildResult.code !== 0 && !buildResult.stdout?.includes('Successfully tagged') && !buildResult.stdout?.includes('naming to docker.io/library/vps_server')) {
            throw new Error(`Lỗi khi Build Docker Image: ${buildResult.stderr || buildResult.stdout || 'Build failed'}`);
        }
        appendDeployLog('✔ Build Image Docker vps_server thành công!');

        // BƯỚC 4: DOCKER RUN
        dockerDeployState.currentStep = 4;
        dockerDeployState.stepName = 'Khởi chạy Container mới (Port 7777/udp)';
        appendDeployLog('\n▶ [BƯỚC 4/4] Khởi động Container mới (live_server) tại cổng 7777/udp...');
        const runResult = await runExecCommand('docker run -d -p 7777:7777/udp --name live_server vps_server');
        if (runResult.code !== 0) {
            throw new Error(`Lỗi khi khởi động Container: ${runResult.stderr || 'Run container failed'}`);
        }
        appendDeployLog('✔ Container live_server đã được khởi động thành công!');

        dockerDeployState.status = 'success';
        dockerDeployState.stepName = 'Hoàn tất Deploy!';
        dockerDeployState.endTime = Date.now();
        const duration = Math.round((dockerDeployState.endTime - dockerDeployState.startTime) / 1000);
        appendDeployLog(`\n🎉 HOÀN TẤT THÀNH CÔNG trong ${duration} giây! Game Server đang online tại cổng 7777/udp.`);

        await writeAdminLog(
            adminUser.username,
            adminUser.displayName,
            'Upload & Deploy Build Game',
            'Docker Manager',
            `Đã tải lên file build ${filename} (${sizeMB} MB), merge và khởi chạy container live_server trong ${duration}s.`
        );

    } catch (err) {
        dockerDeployState.status = 'error';
        dockerDeployState.error = err.message;
        dockerDeployState.endTime = Date.now();
        appendDeployLog(`\n❌ [LỖI TRIỂN KHAI]: ${err.message}`);
        console.error('[DockerDeploy Error]', err);
        // Dọn dẹp nếu còn file tạm
        try { fs.unlinkSync(tempFilePath); } catch (e) {}
        try { fs.rmSync(stagingDir, { recursive: true, force: true }); } catch (e) {}
    } finally {
        dockerDeployState.isDeploying = false;
    }
}

// POST /api/admin/docker/upload-build (Upload file build .zip, .rar, .7z trực tiếp từ PC lên VPS, Tự động Merge & Deploy)
app.post('/api/admin/docker/upload-build', authenticateAdminToken, (req, res) => {
    // Hỗ trợ file nặng hàng GB không bị timeout
    req.setTimeout(30 * 60 * 1000); // 30 phút
    res.setTimeout(30 * 60 * 1000);

    const autoDeploy = req.headers['x-auto-deploy'] === 'true' || req.query.autoDeploy === 'true';
    const filename = req.headers['x-file-name'] ? decodeURIComponent(req.headers['x-file-name']) : 'atlantis_server_build.zip';
    const ext = filename.split('.').pop().toLowerCase();
    const tempFilePath = path.join(os.tmpdir(), `atlantis_server_build_${Date.now()}.${ext}`);
    const stagingDir = path.join(os.tmpdir(), `atlantis_staging_${Date.now()}`);

    const writeStream = fs.createWriteStream(tempFilePath);

    req.pipe(writeStream);

    writeStream.on('finish', async () => {
        try {
            const stats = fs.statSync(tempFilePath);
            const sizeMB = (stats.size / (1024 * 1024)).toFixed(2);

            // Bắt đầu quy trình giải nén & deploy bất đồng bộ trong background
            executeDockerDeployWithFile(req.admin, tempFilePath, stagingDir, filename, sizeMB, ext);

            // Phản hồi NGAY LẬP TỨC để frontend mở modal xem log real-time
            res.json({
                success: true,
                message: `Đã tải lên hoàn tất file build (${sizeMB} MB)! Đang tiến hành giải nén và Deploy trong nền...`,
                isDeploying: true,
                fileSizeMB: sizeMB
            });
        } catch (err) {
            console.error('[UploadBuild Error]', err);
            res.status(500).json({ success: false, message: `Lỗi xử lý file tải lên: ${err.message}` });
        }
    });

    writeStream.on('error', (err) => {
        console.error('[UploadStream Error]', err);
        res.status(500).json({ success: false, message: `Lỗi khi ghi file tải lên: ${err.message}` });
    });
});

// POST /api/admin/docker/action (Restart / Stop / Start Container)
app.post('/api/admin/docker/action', authenticateAdminToken, async (req, res) => {
    const { action } = req.body; // 'restart' | 'stop' | 'start'
    if (!['restart', 'stop', 'start'].includes(action)) {
        return res.status(400).json({ success: false, message: 'Thao tác không hợp lệ.' });
    }

    exec(`docker ${action} live_server`, { timeout: 10000 }, async (err, stdout, stderr) => {
        if (err) {
            return res.status(500).json({
                success: false,
                message: `Lỗi khi thực hiện ${action} container: ${stderr || err.message}`
            });
        }

        const actionText = action === 'restart' ? 'Khởi động lại' : action === 'stop' ? 'Dừng' : 'Bật';
        await writeAdminLog(
            req.admin.username,
            req.admin.displayName,
            `${actionText} Docker Server`,
            'Docker Manager',
            `Đã thực hiện lệnh ${action} trên container live_server.`
        );

        res.json({
            success: true,
            message: `Đã ${actionText.toLowerCase()} container live_server thành công!`
        });
    });
});


// ─── Start ────────────────────────────────────────────────────────────────────

app.listen(PORT, () => {
    console.log(`[Server] Atlantis Auth Backend running on port ${PORT}`);
});
