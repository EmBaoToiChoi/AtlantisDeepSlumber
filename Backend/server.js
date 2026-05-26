require('dotenv').config();
const express = require('express');
const mongoose = require('mongoose');
const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');
const nodemailer = require('nodemailer');
const cors = require('cors');
const User = require('./models/User');
const Room = require('./models/Room');


const app = express();
const PORT = process.env.PORT || 3000;

// ─── Middleware ───────────────────────────────────────────────────────────────
app.use(cors());
app.use(express.json());

// ─── MongoDB ──────────────────────────────────────────────────────────────────
mongoose.connect(process.env.MONGO_URI)
    .then(() => console.log('[DB] MongoDB Atlas connected'))
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
        res.status(500).json({ success: false, message: 'Lỗi khi tạo phòng.' });
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
        res.status(500).json({ success: false, message: 'Lỗi khi tham gia phòng.' });
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
        res.status(500).json({ success: false, message: 'Lỗi khi lấy danh sách phòng.' });
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
        res.status(500).json({ success: false, message: 'Lỗi khi lấy thông tin phòng.' });
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
        res.status(500).json({ success: false, message: 'Lỗi khi rời phòng.' });
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
        res.status(500).json({ success: false, message: 'Lỗi khi lấy trạng thái nhân vật.' });
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
        res.status(500).json({ success: false, message: 'Lỗi khi lưu trạng thái nhân vật.' });
    }
});


// ─── Start ────────────────────────────────────────────────────────────────────

app.listen(PORT, () => {
    console.log(`[Server] Atlantis Auth Backend running on port ${PORT}`);
});
