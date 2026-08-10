// ATLANTIS DATABASE CONTROL PANEL - CORE LOGIC

// Configuration State
let API_URL = 'http://165.99.14.40:3000'; // Mặc định chạy IP VPS
let ADMIN_TOKEN = '';
let ADMIN_USER = null; // { username, displayName, role }
let activeTab = 'tab-overview';
let cachedUsers = [];
let cachedRooms = [];
let cachedLogs = [];
let cachedAdmins = [];
let liveMetricsTimer = null;

// DOM Elements
const gateOverlay = document.getElementById('gateOverlay');
const dashboard = document.getElementById('dashboard');
const inputBackendUrl = document.getElementById('backendUrl');
const inputAdminUsername = document.getElementById('adminUsername');
const inputAdminPassword = document.getElementById('adminPassword');
const btnConnect = document.getElementById('btnConnect');
const btnLogout = document.getElementById('btnLogout');
const btnRefresh = document.getElementById('btnRefresh');

// Header & Actions
const btnHeaderRestartServer = document.getElementById('btnHeaderRestartServer');

// Profile DOM Elements
const txtAdminDisplayName = document.getElementById('txtAdminDisplayName');
const txtAdminUsername = document.getElementById('txtAdminUsername');
const txtAdminAvatar = document.getElementById('txtAdminAvatar');

// Sidebar Tabs
const navItems = document.querySelectorAll('.nav-item');
const tabPanes = document.querySelectorAll('.tab-pane');
const currentTabTitle = document.getElementById('currentTabTitle');
const currentTabDesc = document.getElementById('currentTabDesc');
const navAdminAccounts = document.getElementById('navAdminAccounts');

// Stats Elements (Overview)
const statTotalUsers = document.getElementById('statTotalUsers');
const statVerifiedUsers = document.getElementById('statVerifiedUsers');
const statActiveRooms = document.getElementById('statActiveRooms');
const statTotalRooms = document.getElementById('statTotalRooms');
const statAvgHp = document.getElementById('statAvgHp');
const statAvgDmg = document.getElementById('statAvgDmg');
const statAvgPoints = document.getElementById('statAvgPoints');

// Overview Mini Metrics Elements
const btnQuickViewSystem = document.getElementById('btnQuickViewSystem');
const overviewCpuBar = document.getElementById('overviewCpuBar');
const overviewCpuText = document.getElementById('overviewCpuText');
const overviewCpuCores = document.getElementById('overviewCpuCores');
const overviewRamBar = document.getElementById('overviewRamBar');
const overviewRamText = document.getElementById('overviewRamText');
const overviewRamUsed = document.getElementById('overviewRamUsed');
const overviewNetRx = document.getElementById('overviewNetRx');
const overviewNetTx = document.getElementById('overviewNetTx');
const overviewNetRps = document.getElementById('overviewNetRps');
const overviewUptime = document.getElementById('overviewUptime');
const overviewDbPing = document.getElementById('overviewDbPing');

// System Monitor Toolbar Elements
const toggleLiveMetrics = document.getElementById('toggleLiveMetrics');
const txtDbPing = document.getElementById('txtDbPing');
const dbPingBadge = document.getElementById('dbPingBadge');
const txtMetricsLastUpdated = document.getElementById('txtMetricsLastUpdated');
const btnSystemRestartServer = document.getElementById('btnSystemRestartServer');

// Hardware Metrics Elements (tab-system)
const cpuRadialBar = document.getElementById('cpuRadialBar');
const valCpuPercent = document.getElementById('valCpuPercent');
const valCpuCores = document.getElementById('valCpuCores');
const valCpuSpeed = document.getElementById('valCpuSpeed');
const valCpuLoadAvg = document.getElementById('valCpuLoadAvg');
const valCpuModel = document.getElementById('valCpuModel');

const ramRadialBar = document.getElementById('ramRadialBar');
const valRamPercent = document.getElementById('valRamPercent');
const valRamUsedTotal = document.getElementById('valRamUsedTotal');
const valRamFree = document.getElementById('valRamFree');
const valNodeRss = document.getElementById('valNodeRss');
const valNodeHeap = document.getElementById('valNodeHeap');

const valNetRx = document.getElementById('valNetRx');
const valNetTx = document.getElementById('valNetTx');
const valNetRps = document.getElementById('valNetRps');
const valNetTotalReq = document.getElementById('valNetTotalReq');
const interfaceChipsList = document.getElementById('interfaceChipsList');

const valHostName = document.getElementById('valHostName');
const valHostOs = document.getElementById('valHostOs');
const valNodeVersion = document.getElementById('valNodeVersion');
const valProcessUptime = document.getElementById('valProcessUptime');
const valOsUptime = document.getElementById('valOsUptime');

// Server Operations Panel Elements
const btnOpRestartServer = document.getElementById('btnOpRestartServer');
const btnOpCleanupRooms = document.getElementById('btnOpCleanupRooms');

// Modal Elements - Restart Server Confirmation
const restartConfirmModal = document.getElementById('restartConfirmModal');
const btnCloseRestartModal = document.getElementById('btnCloseRestartModal');
const btnCancelRestart = document.getElementById('btnCancelRestart');
const btnConfirmRestart = document.getElementById('btnConfirmRestart');
const restartReason = document.getElementById('restartReason');

// Fullscreen Reboot Overlay Elements
const rebootOverlay = document.getElementById('rebootOverlay');
const txtRebootTitle = document.getElementById('txtRebootTitle');
const txtRebootSubtitle = document.getElementById('txtRebootSubtitle');
const rebootTimerVal = document.getElementById('rebootTimerVal');
const step1 = document.getElementById('step1');
const step2 = document.getElementById('step2');
const step3 = document.getElementById('step3');
const step3Text = document.getElementById('step3Text');

// User Elements
const searchUserInput = document.getElementById('searchUser');
const filterVerifiedSelect = document.getElementById('filterVerified');
const usersList = document.getElementById('usersList');

// Room Elements
const roomsList = document.getElementById('roomsList');

// Logs Elements
const logsList = document.getElementById('logsList');
const searchLogInput = document.getElementById('searchLog');

// Admin Accounts Elements (Super Admin Only)
const adminsList = document.getElementById('adminsList');

// Modal Elements - User Edit
const editModal = document.getElementById('editModal');
const editForm = document.getElementById('editForm');
const editUserId = document.getElementById('editUserId');
const editDisplayName = document.getElementById('editDisplayName');
const editEmail = document.getElementById('editEmail');
const editIsVerified = document.getElementById('editIsVerified');
const editIsWeapon2Locked = document.getElementById('editIsWeapon2Locked');
const editIsSkillsUnlocked = document.getElementById('editIsSkillsUnlocked');
const editHealth = document.getElementById('editHealth');
const editActiveWeapon = document.getElementById('editActiveWeapon');
const editUpgradePoints = document.getElementById('editUpgradePoints');
const editHpLevel = document.getElementById('editHpLevel');
const editMpLevel = document.getElementById('editMpLevel');
const editCooldownLevel = document.getElementById('editCooldownLevel');
const editDamageLevel = document.getElementById('editDamageLevel');
const inventoryGrid = document.getElementById('inventoryGrid');
const btnCloseModal = document.getElementById('btnCloseModal');
const btnCancelEdit = document.getElementById('btnCancelEdit');

// Modal Elements - First Login
const firstLoginModal = document.getElementById('firstLoginModal');
const btnSubmitFirstLogin = document.getElementById('btnSubmitFirstLogin');
const firstLoginNewPw = document.getElementById('firstLoginNewPw');
const firstLoginPwGroup = document.getElementById('firstLoginPwGroup');

// Modal Elements - Active Change Password
const changePwModal = document.getElementById('changePwModal');
const changePwForm = document.getElementById('changePwForm');
const btnOpenChangePw = document.getElementById('btnOpenChangePw');
const btnClosePwModal = document.getElementById('btnClosePwModal');
const btnCancelChangePw = document.getElementById('btnCancelChangePw');
const pwOld = document.getElementById('pwOld');
const pwNew = document.getElementById('pwNew');
const pwConfirm = document.getElementById('pwConfirm');

// Toast Container
const toastContainer = document.getElementById('toastContainer');

// ─── INITIALIZATION ──────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
    initParticles();
    
    // Tự động điều chỉnh IP nếu ứng dụng chạy trên cùng VPS, loại trừ trường hợp mở bằng file cục bộ
    const host = window.location.hostname;
    if (host && host !== 'localhost' && host !== '127.0.0.1') {
        API_URL = `${window.location.protocol}//${host}:3000`;
        inputBackendUrl.value = API_URL;
    } else {
        API_URL = inputBackendUrl.value || 'http://165.99.14.40:3000';
    }
    loadSavedCredentials();
    setupEventListeners();
});

// Bioluminescent particle effect
function initParticles() {
    const container = document.getElementById('particles');
    const particleCount = 25;
    
    for (let i = 0; i < particleCount; i++) {
        const particle = document.createElement('div');
        particle.classList.add('biolum-particle');
        
        const size = Math.random() * 5 + 2;
        particle.style.width = `${size}px`;
        particle.style.height = `${size}px`;
        particle.style.left = `${Math.random() * 100}vw`;
        particle.style.animationDuration = `${Math.random() * 12 + 8}s`;
        particle.style.animationDelay = `${Math.random() * 10}s`;
        particle.style.opacity = (Math.random() * 0.2 + 0.1).toString();
        
        container.appendChild(particle);
    }
}

// Load credentials from LocalStorage
function loadSavedCredentials() {
    const savedToken = localStorage.getItem('atlantis_admin_token');
    const savedUsername = localStorage.getItem('atlantis_admin_username');
    const savedDisplayName = localStorage.getItem('atlantis_admin_displayname');
    const savedRole = localStorage.getItem('atlantis_admin_role') || 'admin';
    
    if (savedToken && savedUsername && savedDisplayName) {
        ADMIN_TOKEN = savedToken;
        ADMIN_USER = { username: savedUsername, displayName: savedDisplayName, role: savedRole };
        
        showDashboardUI();
        fetchStats();
        fetchSystemMetrics();
        if (!toggleLiveMetrics || toggleLiveMetrics.checked) {
            startLiveMetricsTimer();
        }
    }
}

// Connect & Login
async function loginAdmin(username, password) {
    btnConnect.disabled = true;
    btnConnect.innerHTML = '<span>Đang xác thực...</span>';
    
    try {
        const res = await fetch(`${API_URL}/api/admin/login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ username, password })
        });
        
        const data = await res.json();
        
        if (res.ok) {
            ADMIN_TOKEN = data.token;
            ADMIN_USER = { username: data.username, displayName: data.displayName, role: data.role };
            
            // Save to LocalStorage
            localStorage.setItem('atlantis_admin_token', data.token);
            localStorage.setItem('atlantis_admin_username', data.username);
            localStorage.setItem('atlantis_admin_displayname', data.displayName);
            localStorage.setItem('atlantis_admin_role', data.role);
            
            showToast(`Chào mừng trở lại, ${data.displayName}!`, 'success');
            
            showDashboardUI();
            fetchStats();
            fetchSystemMetrics();
            if (!toggleLiveMetrics || toggleLiveMetrics.checked) {
                startLiveMetricsTimer();
            }
            if (data.isFirstLogin) {
                firstLoginModal.classList.add('show');
            }
        } else {
            showToast(data.message || 'Tài khoản hoặc mật khẩu không chính xác.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Không thể kết nối đến Backend Server. Hãy chắc chắn Server đã chạy và mở port 3000.', 'error');
    } finally {
        btnConnect.disabled = false;
        btnConnect.innerHTML = '<span>ĐĂNG NHẬP HỆ THỐNG</span>';
    }
}

function showDashboardUI() {
    // Populate Admin info box with role
    txtAdminDisplayName.textContent = ADMIN_USER.displayName;
    const roleTitle = ADMIN_USER.role === 'superadmin' ? 'Super Admin' : 'Admin';
    txtAdminUsername.textContent = `@${ADMIN_USER.username} (${roleTitle})`;
    txtAdminAvatar.textContent = ADMIN_USER.displayName.charAt(0).toUpperCase();

    // Toggle navigation tab for Admin Management based on permissions
    if (ADMIN_USER.role === 'superadmin') {
        navAdminAccounts.style.display = 'flex';
    } else {
        navAdminAccounts.style.display = 'none';
    }

    // UI Transition
    gateOverlay.style.opacity = '0';
    setTimeout(() => {
        gateOverlay.style.display = 'none';
        dashboard.style.display = 'grid';
    }, 300);
    
    document.getElementById('txtConnectionState').textContent = 'Đã kết nối';
    document.getElementById('txtConnectionState').parentElement.querySelector('.status-dot').className = 'status-dot online';
}

// ─── EVENTS SETUP ────────────────────────────────────────────────────────────
function setupEventListeners() {
    btnConnect.addEventListener('click', () => {
        const username = inputAdminUsername.value.trim();
        const password = inputAdminPassword.value.trim();
        if (!username || !password) {
            showToast('Vui lòng nhập đầy đủ tài khoản và mật khẩu!', 'info');
            return;
        }
        loginAdmin(username, password);
    });
    
    inputAdminPassword.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') btnConnect.click();
    });

    btnLogout.addEventListener('click', () => {
        localStorage.removeItem('atlantis_admin_token');
        localStorage.removeItem('atlantis_admin_username');
        localStorage.removeItem('atlantis_admin_displayname');
        localStorage.removeItem('atlantis_admin_role');
        location.reload();
    });

    btnRefresh.addEventListener('click', () => {
        const icon = btnRefresh.querySelector('svg');
        icon.classList.add('spin');
        loadTabContent(activeTab).finally(() => {
            setTimeout(() => icon.classList.remove('spin'), 500);
        });
    });

    navItems.forEach(item => {
        item.addEventListener('click', () => {
            const target = item.getAttribute('data-tab');
            switchTab(target);
        });
    });

    // Server Restart buttons
    if (btnHeaderRestartServer) btnHeaderRestartServer.addEventListener('click', openRestartModal);
    if (btnSystemRestartServer) btnSystemRestartServer.addEventListener('click', openRestartModal);
    if (btnOpRestartServer) btnOpRestartServer.addEventListener('click', openRestartModal);
    if (btnCloseRestartModal) btnCloseRestartModal.addEventListener('click', closeRestartModal);
    if (btnCancelRestart) btnCancelRestart.addEventListener('click', closeRestartModal);
    if (restartConfirmModal) {
        restartConfirmModal.addEventListener('click', (e) => {
            if (e.target === restartConfirmModal) closeRestartModal();
        });
    }
    if (btnConfirmRestart) btnConfirmRestart.addEventListener('click', handleConfirmRestart);

    // Clean empty rooms button
    if (btnOpCleanupRooms) btnOpCleanupRooms.addEventListener('click', handleCleanupRooms);

    // Quick View System button in Overview
    if (btnQuickViewSystem) {
        btnQuickViewSystem.addEventListener('click', () => {
            switchTab('tab-system');
        });
    }

    // Live Metrics Toggle
    if (toggleLiveMetrics) {
        toggleLiveMetrics.addEventListener('change', () => {
            if (toggleLiveMetrics.checked) {
                startLiveMetricsTimer();
                showToast('Đã bật chế độ cập nhật phần cứng trực tiếp (3s).', 'info');
            } else {
                stopLiveMetricsTimer();
                showToast('Đã tắt tự động cập nhật phần cứng.', 'info');
            }
        });
    }

    searchUserInput.addEventListener('input', applyUserFilters);
    filterVerifiedSelect.addEventListener('change', applyUserFilters);
    searchLogInput.addEventListener('input', applyLogFilters);

    btnCloseModal.addEventListener('click', closeModal);
    btnCancelEdit.addEventListener('click', closeModal);
    editModal.addEventListener('click', (e) => {
        if (e.target === editModal) closeModal();
    });
    editForm.addEventListener('submit', handleFormSubmit);

    btnOpenChangePw.addEventListener('click', () => {
        changePwModal.classList.add('show');
    });
    btnClosePwModal.addEventListener('click', closeChangePwModal);
    btnCancelChangePw.addEventListener('click', closeChangePwModal);
    changePwModal.addEventListener('click', (e) => {
        if (e.target === changePwModal) closeChangePwModal();
    });
    changePwForm.addEventListener('submit', handleChangePwSubmit);

    const firstLoginRadios = document.querySelectorAll('input[name="firstLoginChoice"]');
    firstLoginRadios.forEach(radio => {
        radio.addEventListener('change', () => {
            if (radio.value === 'change') {
                firstLoginPwGroup.style.display = 'block';
                firstLoginNewPw.required = true;
            } else {
                firstLoginPwGroup.style.display = 'none';
                firstLoginNewPw.required = false;
                firstLoginNewPw.value = '';
            }
        });
    });

    btnSubmitFirstLogin.addEventListener('click', handleFirstLoginAction);
}

// ─── TAB NAVIGATION LOGIC ──────────────────────────────────────────────────
function switchTab(tabId) {
    activeTab = tabId;
    
    navItems.forEach(btn => {
        if (btn.getAttribute('data-tab') === tabId) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    tabPanes.forEach(pane => {
        if (pane.id === tabId) {
            pane.classList.add('active');
        } else {
            pane.classList.remove('active');
        }
    });

    if (tabId === 'tab-overview') {
        currentTabTitle.textContent = 'Hệ thống điều khiển trung tâm';
        currentTabDesc.textContent = 'Thống kê trạng thái thời gian thực của cơ sở dữ liệu và máy chủ game.';
    } else if (tabId === 'tab-system') {
        currentTabTitle.textContent = 'Giám sát Hệ thống & Tài nguyên Máy chủ';
        currentTabDesc.textContent = 'Theo dõi CPU, RAM, Network I/O, thời gian hoạt động và điều khiển tiến trình Backend.';
    } else if (tabId === 'tab-users') {
        currentTabTitle.textContent = 'Quản lý Tài khoản & Trạng thái';
        currentTabDesc.textContent = 'Tra cứu người chơi, kích hoạt tài khoản và chỉnh sửa các chỉ số nâng cấp game.';
    } else if (tabId === 'tab-rooms') {
        currentTabTitle.textContent = 'Giám sát Phòng chơi (Lobby Monitor)';
        currentTabDesc.textContent = 'Danh sách các phòng chơi đang mở, đang hoạt động hoặc đã kết thúc.';
    } else if (tabId === 'tab-logs') {
        currentTabTitle.textContent = 'Nhật ký hoạt động Quản trị';
        currentTabDesc.textContent = 'Lịch sử thay đổi hệ thống và dữ liệu game của các Admin.';
    } else if (tabId === 'tab-admin-accounts') {
        currentTabTitle.textContent = 'Cấu hình Quyền Quản trị viên';
        currentTabDesc.textContent = 'Quản lý tài khoản quản trị hệ thống và quyền đặt lại mật khẩu.';
    }

    loadTabContent(tabId);
}

async function loadTabContent(tabId) {
    if (tabId === 'tab-overview') {
        await Promise.all([fetchStats(), fetchSystemMetrics()]);
        if (!toggleLiveMetrics || toggleLiveMetrics.checked) {
            startLiveMetricsTimer();
        }
    } else if (tabId === 'tab-system') {
        await fetchSystemMetrics();
        if (!toggleLiveMetrics || toggleLiveMetrics.checked) {
            startLiveMetricsTimer();
        }
    } else {
        stopLiveMetricsTimer();
        if (tabId === 'tab-users') {
            await fetchUsers();
        } else if (tabId === 'tab-rooms') {
            await fetchRooms();
        } else if (tabId === 'tab-logs') {
            await fetchLogs();
        } else if (tabId === 'tab-admin-accounts') {
            await fetchAdminAccounts();
        }
    }
}

// Helper headers
function getHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${ADMIN_TOKEN}`
    };
}

// ─── API OPERATIONS ──────────────────────────────────────────────────────────

// Fetch Stats
async function fetchStats() {
    try {
        const res = await fetch(`${API_URL}/api/admin/stats`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            updateStatsUI(data.stats);
        } else {
            showToast('Lỗi khi tải thống kê cơ sở dữ liệu.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Không thể tải thống kê. Phiên làm việc có thể đã hết hạn.', 'error');
    }
}

function updateStatsUI(stats) {
    statTotalUsers.textContent = stats.totalUsers;
    statVerifiedUsers.textContent = stats.verifiedUsers;
    statActiveRooms.textContent = stats.activeRooms;
    statTotalRooms.textContent = stats.totalRooms;
    statAvgHp.textContent = stats.avgHpLevel.toFixed(1);
    statAvgDmg.textContent = stats.avgDamageLevel.toFixed(1);
    statAvgPoints.textContent = stats.avgUpgradePoints.toFixed(1);
}

// Fetch Users List
async function fetchUsers() {
    usersList.innerHTML = `<tr><td colspan="7" class="loading-row">Đang truy vấn dữ liệu...</td></tr>`;
    try {
        const res = await fetch(`${API_URL}/api/admin/users`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            cachedUsers = data.users;
            applyUserFilters();
        } else {
            showToast('Lỗi khi tải danh sách người chơi.', 'error');
            usersList.innerHTML = `<tr><td colspan="7" class="loading-row error-row">Không thể tải dữ liệu.</td></tr>`;
        }
    } catch (err) {
        console.error(err);
        usersList.innerHTML = `<tr><td colspan="7" class="loading-row error-row">Lỗi kết nối máy chủ.</td></tr>`;
    }
}

function applyUserFilters() {
    const query = searchUserInput.value.toLowerCase().trim();
    const filter = filterVerifiedSelect.value;
    
    const filtered = cachedUsers.filter(user => {
        const matchesQuery = user.displayName.toLowerCase().includes(query) || 
                             user.email.toLowerCase().includes(query) ||
                             user._id.includes(query);
                             
        const matchesFilter = filter === 'all' || 
                              (filter === 'verified' && user.isVerified) || 
                              (filter === 'unverified' && !user.isVerified);
                              
        return matchesQuery && matchesFilter;
    });

    renderUsers(filtered);
}

function renderUsers(users) {
    if (users.length === 0) {
        usersList.innerHTML = `<tr><td colspan="7" class="loading-row">Không tìm thấy người chơi phù hợp.</td></tr>`;
        return;
    }

    usersList.innerHTML = users.map(user => {
        const state = user.playerState || {};
        const regDate = new Date(user.createdAt).toLocaleDateString('vi-VN', {
            year: 'numeric', month: 'short', day: 'numeric'
        });
        
        const verificationBadge = user.isVerified 
            ? `<span class="badge badge-success">Đã kích hoạt</span>` 
            : `<span class="badge badge-danger">Chưa xác nhận</span>`;

        return `
            <tr>
                <td>
                    <div class="player-avatar">
                        <div class="player-avatar-circle">${user.displayName.charAt(0).toUpperCase()}</div>
                        <div>
                            <div class="font-semibold">${escapeHTML(user.displayName)}</div>
                            <div class="text-secondary text-sm font-mono" style="font-size:10px;">ID: ${user._id}</div>
                        </div>
                    </div>
                </td>
                <td class="font-mono">${escapeHTML(user.email)}</td>
                <td>${verificationBadge}</td>
                <td>
                    <span class="text-highlight font-semibold">${state.health ?? 100}</span> HP
                </td>
                <td class="font-semibold text-highlight" style="color:var(--warning);">${state.upgradePoints ?? 0} pts</td>
                <td class="text-secondary font-mono">${regDate}</td>
                <td class="actions">
                    <button class="btn-edit" onclick="openEditModal('${user._id}')">Sửa chỉ số</button>
                    <button class="btn-danger" onclick="deleteUser('${user._id}', '${escapeHTML(user.displayName)}')">Xóa</button>
                </td>
            </tr>
        `;
    }).join('');
}

// Fetch Rooms List
async function fetchRooms() {
    roomsList.innerHTML = `<tr><td colspan="8" class="loading-row">Đang tải danh sách phòng...</td></tr>`;
    try {
        const res = await fetch(`${API_URL}/api/admin/rooms`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            cachedRooms = data.rooms;
            renderRooms(cachedRooms);
        } else {
            showToast('Lỗi khi tải danh sách phòng chơi.', 'error');
            roomsList.innerHTML = `<tr><td colspan="8" class="loading-row error-row">Không thể tải dữ liệu.</td></tr>`;
        }
    } catch (err) {
        console.error(err);
        roomsList.innerHTML = `<tr><td colspan="8" class="loading-row error-row">Lỗi kết nối máy chủ.</td></tr>`;
    }
}

function renderRooms(rooms) {
    if (rooms.length === 0) {
        roomsList.innerHTML = `<tr><td colspan="8" class="loading-row">Không có phòng chơi nào đang hoạt động.</td></tr>`;
        return;
    }

    roomsList.innerHTML = rooms.map(room => {
        const hostName = room.host ? room.host.displayName : 'Không xác định';
        const dateCreated = new Date(room.createdAt).toLocaleTimeString('vi-VN', {
            hour: '2-digit', minute: '2-digit', second: '2-digit'
        });

        let statusBadge = '';
        if (room.status === 'waiting') {
            statusBadge = `<span class="badge badge-success">Đang chờ</span>`;
        } else if (room.status === 'playing') {
            statusBadge = `<span class="badge badge-warning">Đang chơi</span>`;
        } else {
            statusBadge = `<span class="badge badge-primary">${room.status}</span>`;
        }

        const privateBadge = room.isPrivate 
            ? `<span class="badge badge-danger">Khóa (Mật khẩu)</span>` 
            : `<span class="badge badge-primary">Công khai</span>`;

        const playersListHtml = room.players.map(p => {
            const isHost = room.host && room.host._id === p.user;
            return `<span class="player-slot-badge ${isHost ? 'slot-host' : ''}" title="Slot ${p.slot}">
                ${escapeHTML(p.displayName)} (Slot ${p.slot})
            </span>`;
        }).join('');

        return `
            <tr>
                <td class="font-mono font-semibold text-highlight">${room.roomId}</td>
                <td>${escapeHTML(room.roomName)}</td>
                <td>${escapeHTML(hostName)}</td>
                <td>
                    <div class="room-players-badges">
                        ${playersListHtml}
                    </div>
                </td>
                <td>${statusBadge}</td>
                <td>${privateBadge}</td>
                <td class="font-mono text-secondary">${dateCreated}</td>
                <td class="actions">
                    <button class="btn-danger" onclick="deleteRoom('${room.roomId}')">Giải tán phòng</button>
                </td>
            </tr>
        `;
    }).join('');
}

// Fetch Admin Logs List
async function fetchLogs() {
    logsList.innerHTML = `<tr><td colspan="5" class="loading-row">Đang tải lịch sử hoạt động...</td></tr>`;
    try {
        const res = await fetch(`${API_URL}/api/admin/logs`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            cachedLogs = data.logs;
            applyLogFilters();
        } else {
            showToast('Lỗi khi tải nhật ký hoạt động.', 'error');
            logsList.innerHTML = `<tr><td colspan="5" class="loading-row error-row">Không thể tải dữ liệu.</td></tr>`;
        }
    } catch (err) {
        console.error(err);
        logsList.innerHTML = `<tr><td colspan="5" class="loading-row error-row">Lỗi kết nối máy chủ.</td></tr>`;
    }
}

function applyLogFilters() {
    const query = searchLogInput.value.toLowerCase().trim();
    
    const filtered = cachedLogs.filter(log => {
        return log.adminDisplayName.toLowerCase().includes(query) ||
               log.adminUsername.toLowerCase().includes(query) ||
               log.action.toLowerCase().includes(query) ||
               log.target.toLowerCase().includes(query) ||
               log.details.toLowerCase().includes(query);
    });

    renderLogs(filtered);
}

function renderLogs(logs) {
    if (logs.length === 0) {
        logsList.innerHTML = `<tr><td colspan="5" class="loading-row">Không tìm thấy bản ghi hoạt động nào.</td></tr>`;
        return;
    }

    logsList.innerHTML = logs.map(log => {
        const timeStr = new Date(log.createdAt).toLocaleString('vi-VN', {
            year: 'numeric', month: '2-digit', day: '2-digit',
            hour: '2-digit', minute: '2-digit', second: '2-digit'
        });

        let badgeClass = 'badge-action-other';
        if (log.action === 'Sửa chỉ số') badgeClass = 'badge-action-edit';
        else if (log.action === 'Xóa tài khoản' || log.action === 'Giải tán phòng') badgeClass = 'badge-action-delete';
        else if (log.action === 'Đổi mật khẩu') badgeClass = 'badge-action-pwd';
        else if (log.action === 'Reset mật khẩu') badgeClass = 'badge-action-delete';

        return `
            <tr>
                <td class="font-mono text-secondary" style="font-size:12px; white-space:nowrap;">${timeStr}</td>
                <td>
                    <div class="font-semibold">${escapeHTML(log.adminDisplayName)}</div>
                    <div class="text-secondary text-sm font-mono" style="font-size:10px;">@${log.adminUsername}</div>
                </td>
                <td>
                    <span class="badge-action ${badgeClass}">${escapeHTML(log.action)}</span>
                </td>
                <td class="font-semibold" style="font-size: 13px;">${escapeHTML(log.target)}</td>
                <td class="text-secondary" style="font-size: 13px; line-height: 1.4;">${escapeHTML(log.details)}</td>
            </tr>
        `;
    }).join('');
}

// Fetch Admin Accounts (Super Admin Only)
async function fetchAdminAccounts() {
    adminsList.innerHTML = `<tr><td colspan="6" class="loading-row">Đang truy vấn danh sách admin...</td></tr>`;
    try {
        const res = await fetch(`${API_URL}/api/admin/accounts`, {
            headers: getHeaders()
        });
        
        if (res.ok) {
            const data = await res.json();
            cachedAdmins = data.admins;
            renderAdmins(cachedAdmins);
        } else {
            showToast('Không có quyền truy cập hoặc lỗi khi lấy danh sách Admin.', 'error');
            adminsList.innerHTML = `<tr><td colspan="6" class="loading-row error-row">Lỗi tải dữ liệu.</td></tr>`;
        }
    } catch (err) {
        console.error(err);
        adminsList.innerHTML = `<tr><td colspan="6" class="loading-row error-row">Lỗi kết nối máy chủ.</td></tr>`;
    }
}

function renderAdmins(admins) {
    if (admins.length === 0) {
        adminsList.innerHTML = `<tr><td colspan="6" class="loading-row">Không tìm thấy tài khoản quản trị.</td></tr>`;
        return;
    }

    adminsList.innerHTML = admins.map(adm => {
        const regDate = new Date(adm.createdAt || Date.now()).toLocaleDateString('vi-VN', {
            year: 'numeric', month: 'short', day: 'numeric'
        });

        const roleBadge = adm.role === 'superadmin'
            ? `<span class="badge badge-role-superadmin">Super Admin</span>`
            : `<span class="badge badge-role-admin">Admin</span>`;

        const statusBadge = adm.isFirstLogin
            ? `<span class="badge badge-warning">Mật khẩu mặc định</span>`
            : `<span class="badge badge-success">Hoạt động tốt</span>`;

        // Don't allow self-reset
        const isSelf = ADMIN_USER.username === adm.username;
        const actionBtn = isSelf 
            ? `<span class="text-secondary text-sm font-mono">Tài khoản hiện tại</span>`
            : `<button class="btn-danger" onclick="resetAdminPassword('${adm._id}', '${escapeHTML(adm.username)}')">Reset mật khẩu</button>`;

        return `
            <tr>
                <td class="font-mono font-semibold text-highlight">@${escapeHTML(adm.username)}</td>
                <td class="font-semibold">${escapeHTML(adm.displayName)}</td>
                <td>${roleBadge}</td>
                <td>${statusBadge}</td>
                <td class="font-mono text-secondary">${regDate}</td>
                <td class="actions">
                    ${actionBtn}
                </td>
            </tr>
        `;
    }).join('');
}

// Reset password for an admin (Super Admin Only)
async function resetAdminPassword(adminId, username) {
    if (!confirm(`Cảnh báo quản trị!\nBạn có chắc chắn muốn đặt lại mật khẩu của quản trị viên @${username} về mặc định "123456"?\nHọ sẽ được yêu cầu đổi mật khẩu lại ở lần đăng nhập tiếp theo.`)) {
        return;
    }

    try {
        const res = await fetch(`${API_URL}/api/admin/accounts/${adminId}/reset-password`, {
            method: 'POST',
            headers: getHeaders()
        });

        const data = await res.json();
        
        if (res.ok) {
            showToast(data.message, 'success');
            fetchAdminAccounts(); // refresh list
        } else {
            showToast(data.message || 'Lỗi khi reset mật khẩu.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi kết nối máy chủ.', 'error');
    }
}

// ─── DELETE OPERATIONS ───────────────────────────────────────────────────────
async function deleteUser(userId, displayName) {
    if (!confirm(`Cảnh báo nguy hiểm!\nBạn có chắc chắn muốn XÓA VĨNH VIỄN tài khoản người chơi: ${displayName}?\nHành động này sẽ được ghi vào nhật ký hệ thống.`)) {
        return;
    }

    try {
        const res = await fetch(`${API_URL}/api/admin/users/${userId}`, {
            method: 'DELETE',
            headers: getHeaders()
        });

        if (res.ok) {
            showToast(`Đã xóa thành công người chơi ${displayName}`, 'success');
            fetchUsers();
        } else {
            const err = await res.json().catch(() => ({}));
            showToast(err.message || 'Lỗi khi xóa người dùng.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi mạng khi xóa người dùng.', 'error');
    }
}

async function deleteRoom(roomId) {
    if (!confirm(`Bạn có chắc muốn cưỡng chế GIẢI TÁN phòng chơi: ${roomId}?`)) {
        return;
    }

    try {
        const res = await fetch(`${API_URL}/api/admin/rooms/${roomId}`, {
            method: 'DELETE',
            headers: getHeaders()
        });

        if (res.ok) {
            showToast(`Đã đóng phòng chơi ${roomId} thành công`, 'success');
            fetchRooms();
        } else {
            const err = await res.json().catch(() => ({}));
            showToast(err.message || 'Lỗi khi giải tán phòng.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi mạng khi giải tán phòng.', 'error');
    }
}

// ─── FIRST LOGIN FLOW ────────────────────────────────────────────────────────
async function handleFirstLoginAction() {
    const choice = document.querySelector('input[name="firstLoginChoice"]:checked').value;
    const newPw = firstLoginNewPw.value.trim();
    
    if (choice === 'change') {
        if (!newPw || newPw.length < 6) {
            showToast('Mật khẩu mới phải có ít nhất 6 ký tự!', 'info');
            return;
        }
    }

    btnSubmitFirstLogin.disabled = true;
    btnSubmitFirstLogin.textContent = 'Đang xử lý...';

    try {
        const res = await fetch(`${API_URL}/api/admin/first-login-action`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify({
                actionType: choice,
                newPassword: choice === 'change' ? newPw : null
            })
        });

        if (res.ok) {
            showToast(choice === 'change' ? 'Đổi mật khẩu thành công!' : 'Đã bỏ qua đổi mật khẩu lần đầu.', 'success');
            firstLoginModal.classList.remove('show');
            showDashboardUI();
            fetchStats();
        } else {
            const data = await res.json().catch(() => ({}));
            showToast(data.message || 'Lỗi khi xử lý thao tác lần đầu.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi kết nối máy chủ.', 'error');
    } finally {
        btnSubmitFirstLogin.disabled = false;
        btnSubmitFirstLogin.textContent = 'Xác nhận lựa chọn';
    }
}

// ─── ACTIVE CHANGE PASSWORD ──────────────────────────────────────────────────
function closeChangePwModal() {
    changePwModal.classList.remove('show');
    changePwForm.reset();
}

async function handleChangePwSubmit(e) {
    e.preventDefault();
    
    const oldVal = pwOld.value.trim();
    const newVal = pwNew.value.trim();
    const confirmVal = pwConfirm.value.trim();

    if (newVal.length < 6) {
        showToast('Mật khẩu mới phải dài tối thiểu 6 ký tự!', 'info');
        return;
    }
    if (newVal !== confirmVal) {
        showToast('Mật khẩu mới nhập lại không khớp!', 'info');
        return;
    }

    try {
        const res = await fetch(`${API_URL}/api/admin/change-password`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify({
                oldPassword: oldVal,
                newPassword: newVal
            })
        });

        const data = await res.json();
        
        if (res.ok) {
            showToast('Đổi mật khẩu quản trị viên thành công!', 'success');
            closeChangePwModal();
        } else {
            showToast(data.message || 'Đổi mật khẩu thất bại.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi mạng khi cập nhật mật khẩu.', 'error');
    }
}

// ─── PLAYER STATE EDITOR MODAL ────────────────────────────────────────────────
function openEditModal(userId) {
    const user = cachedUsers.find(u => u._id === userId);
    if (!user) return;

    const state = user.playerState || {};
    
    editUserId.value = user._id;
    editDisplayName.value = user.displayName;
    editEmail.value = user.email;
    editIsVerified.checked = !!user.isVerified;
    
    editIsWeapon2Locked.checked = state.isWeapon2Locked !== undefined ? state.isWeapon2Locked : true;
    editIsSkillsUnlocked.checked = !!state.isSkillsUnlocked;
    editHealth.value = state.health !== undefined ? state.health : 100;
    editActiveWeapon.value = state.activeWeaponIndex !== undefined ? state.activeWeaponIndex : 1;
    editUpgradePoints.value = state.upgradePoints !== undefined ? state.upgradePoints : 5;
    
    editHpLevel.value = state.hpLevel || 0;
    editMpLevel.value = state.mpLevel || 0;
    editCooldownLevel.value = state.cooldownLevel || 0;
    editDamageLevel.value = state.damageLevel || 0;

    const inventory = state.inventorySlots || ["", "", "", "", "", "", "", "", "", ""];
    inventoryGrid.innerHTML = '';
    
    for (let i = 0; i < 10; i++) {
        const val = inventory[i] || '';
        const itemSlot = document.createElement('div');
        itemSlot.classList.add('inventory-slot-wrapper');
        itemSlot.innerHTML = `
            <span>Slot ${i + 1}</span>
            <input type="text" class="inventory-slot-input" data-index="${i}" value="${escapeHTML(val)}" placeholder="Trống">
        `;
        inventoryGrid.appendChild(itemSlot);
    }

    editModal.classList.add('show');
}

function closeModal() {
    editModal.classList.remove('show');
}

async function handleFormSubmit(e) {
    e.preventDefault();
    
    const userId = editUserId.value;
    const displayName = editDisplayName.value.trim();
    const email = editEmail.value.trim();
    const isVerified = editIsVerified.checked;

    const inventoryInputs = document.querySelectorAll('.inventory-slot-input');
    const inventorySlots = Array(10).fill("");
    inventoryInputs.forEach(input => {
        const idx = parseInt(input.getAttribute('data-index'));
        inventorySlots[idx] = input.value.trim();
    });

    const reqBody = {
        displayName,
        email,
        isVerified,
        playerState: {
            health: parseInt(editHealth.value),
            activeWeaponIndex: parseInt(editActiveWeapon.value),
            isWeapon2Locked: editIsWeapon2Locked.checked,
            isSkillsUnlocked: editIsSkillsUnlocked.checked,
            upgradePoints: parseInt(editUpgradePoints.value),
            hpLevel: parseInt(editHpLevel.value),
            mpLevel: parseInt(editMpLevel.value),
            cooldownLevel: parseInt(editCooldownLevel.value),
            damageLevel: parseInt(editDamageLevel.value),
            inventorySlots
        }
    };

    try {
        const res = await fetch(`${API_URL}/api/admin/users/${userId}`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify(reqBody)
        });

        if (res.ok) {
            showToast('Đã lưu các thay đổi chỉ số thành công!', 'success');
            closeModal();
            fetchUsers();
        } else {
            const data = await res.json().catch(() => ({}));
            showToast(data.message || 'Lỗi khi cập nhật chỉ số người chơi.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi mạng khi lưu dữ liệu chỉnh sửa.', 'error');
    }
}

// ─── TOAST CONTROLLER ────────────────────────────────────────────────────────
function showToast(message, type = 'info') {
    const toast = document.createElement('div');
    toast.classList.add('toast', type);
    
    let svgIcon = '';
    if (type === 'success') {
        svgIcon = `<svg width="18" height="18" fill="var(--green)" viewBox="0 0 24 24"><path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z"/></svg>`;
    } else if (type === 'error') {
        svgIcon = `<svg width="18" height="18" fill="var(--danger)" viewBox="0 0 24 24"><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg>`;
    } else {
        svgIcon = `<svg width="18" height="18" fill="var(--cyan)" viewBox="0 0 24 24"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z"/></svg>`;
    }

    toast.innerHTML = `
        <div class="toast-content">
            ${svgIcon}
            <span>${message}</span>
        </div>
        <button class="toast-close">&times;</button>
    `;

    toastContainer.appendChild(toast);

    const closeBtn = toast.querySelector('.toast-close');
    closeBtn.addEventListener('click', () => {
        removeToast(toast);
    });

    setTimeout(() => {
        removeToast(toast);
    }, 4000);
}

function removeToast(toast) {
    toast.style.transform = 'translateX(120%)';
    toast.style.opacity = '0';
    setTimeout(() => {
        if (toast.parentNode) {
            toast.parentNode.removeChild(toast);
        }
    }, 300);
}

// ─── SYSTEM METRICS & HARDWARE MONITOR ───────────────────────────────────────

async function fetchSystemMetrics() {
    if (!ADMIN_TOKEN) return;
    try {
        const res = await fetch(`${API_URL}/api/admin/system-metrics`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            if (data.success && data.metrics) {
                updateSystemMetricsUI(data.metrics);
            }
        }
    } catch (err) {
        console.error('[FetchMetrics Error]', err);
    }
}

function updateSystemMetricsUI(metrics) {
    if (!metrics) return;
    const { cpu, memory, network, database, system } = metrics;

    // 1. CPU Metrics
    const cpuPercent = cpu?.usagePercent || 0;
    if (valCpuPercent) valCpuPercent.textContent = `${cpuPercent.toFixed(1)}%`;
    if (valCpuCores) valCpuCores.textContent = `${cpu.cores || 1} Nhân (${(cpu.cores || 1) * 2} Luồng)`;
    if (valCpuSpeed) valCpuSpeed.textContent = `${cpu.speed || 0} MHz`;
    if (valCpuLoadAvg) valCpuLoadAvg.textContent = cpu.loadAvg ? cpu.loadAvg.map(n => n.toFixed(2)).join(', ') : '0.00, 0.00, 0.00';
    if (valCpuModel) valCpuModel.textContent = cpu.model || 'Standard CPU';
    updateRadialGauge(cpuRadialBar, cpuPercent);

    // 2. RAM & Memory Metrics
    const ramPercent = memory?.usagePercent || 0;
    if (valRamPercent) valRamPercent.textContent = `${ramPercent.toFixed(1)}%`;
    if (valRamUsedTotal) valRamUsedTotal.textContent = `${formatBytes(memory.usedBytes)} / ${formatBytes(memory.totalBytes)}`;
    if (valRamFree) valRamFree.textContent = formatBytes(memory.freeBytes);
    if (valNodeRss) valNodeRss.textContent = `${memory.process?.rssMB || 0} MB`;
    if (valNodeHeap) valNodeHeap.textContent = `${memory.process?.heapUsedMB || 0} / ${memory.process?.heapTotalMB || 0} MB`;
    updateRadialGauge(ramRadialBar, ramPercent);

    // 3. Network & Traffic Metrics
    if (valNetRx) valNetRx.textContent = formatBytes(network?.totalBytesRx || 0);
    if (valNetTx) valNetTx.textContent = formatBytes(network?.totalBytesTx || 0);
    if (valNetRps) valNetRps.textContent = `${network?.requestsPerSec || 0} req/s`;
    if (valNetTotalReq) valNetTotalReq.textContent = (network?.totalRequests || 0).toLocaleString();

    if (interfaceChipsList && network?.interfaces) {
        if (network.interfaces.length > 0) {
            interfaceChipsList.innerHTML = network.interfaces.map(iface => `
                <div class="interface-chip" title="MAC: ${escapeHTML(iface.mac || 'N/A')}">
                    <strong>${escapeHTML(iface.name)}:</strong> ${escapeHTML(iface.address)}
                </div>
            `).join('');
        } else {
            interfaceChipsList.innerHTML = '<span class="text-muted text-sm">Không phát hiện card mạng IPv4.</span>';
        }
    }

    // 4. Host & Runtime Info
    if (valHostName) valHostName.textContent = system?.hostname || 'atlantis-server';
    if (valHostOs) valHostOs.textContent = `${system?.platform || 'Linux'} (${system?.arch || 'x64'}) - ${system?.release || ''}`;
    if (valNodeVersion) valNodeVersion.textContent = system?.nodeVersion || 'v20.x';
    if (valProcessUptime) valProcessUptime.textContent = formatUptime(system?.processUptimeSec);
    if (valOsUptime) valOsUptime.textContent = formatUptime(system?.osUptimeSec);

    // 5. Toolbar Ping & Last Updated
    if (txtDbPing) {
        const ping = database?.pingLatencyMs ?? -1;
        txtDbPing.textContent = ping >= 0 ? `${ping} ms` : 'Offline';
        if (dbPingBadge) {
            const dot = dbPingBadge.querySelector('.ping-dot');
            if (dot) {
                dot.style.background = ping >= 0 && ping < 80 ? 'var(--green)' : ping < 200 ? 'var(--warning)' : 'var(--danger)';
            }
        }
    }
    if (txtMetricsLastUpdated) {
        const now = new Date();
        txtMetricsLastUpdated.textContent = `Cập nhật: ${now.toLocaleTimeString('vi-VN')}`;
    }

    // 6. Overview Tab Mini Hardware Widgets
    if (overviewCpuBar) overviewCpuBar.style.width = `${Math.min(100, Math.max(0, cpuPercent))}%`;
    if (overviewCpuText) overviewCpuText.textContent = `${cpuPercent.toFixed(1)}%`;
    if (overviewCpuCores) overviewCpuCores.textContent = `${cpu.cores || 1} Cores`;

    if (overviewRamBar) overviewRamBar.style.width = `${Math.min(100, Math.max(0, ramPercent))}%`;
    if (overviewRamText) overviewRamText.textContent = `${ramPercent.toFixed(1)}%`;
    if (overviewRamUsed) overviewRamUsed.textContent = `${formatBytes(memory.usedBytes)}`;

    if (overviewNetRx) overviewNetRx.textContent = formatBytes(network?.totalBytesRx || 0);
    if (overviewNetTx) overviewNetTx.textContent = formatBytes(network?.totalBytesTx || 0);
    if (overviewNetRps) overviewNetRps.textContent = `${network?.requestsPerSec || 0} req/s`;
    if (overviewUptime) overviewUptime.textContent = formatUptime(system?.processUptimeSec);
    if (overviewDbPing) overviewDbPing.textContent = `Ping: ${database?.pingLatencyMs >= 0 ? database.pingLatencyMs : 0}ms`;
}

function updateRadialGauge(circleElem, percent) {
    if (!circleElem) return;
    const circumference = 263.89; // 2 * pi * 42
    const p = Math.min(100, Math.max(0, percent));
    const offset = circumference - (p / 100) * circumference;
    circleElem.style.strokeDashoffset = offset;
}

function startLiveMetricsTimer() {
    stopLiveMetricsTimer();
    liveMetricsTimer = setInterval(() => {
        if (activeTab === 'tab-system' || activeTab === 'tab-overview') {
            fetchSystemMetrics();
        }
    }, 3000);
}

function stopLiveMetricsTimer() {
    if (liveMetricsTimer) {
        clearInterval(liveMetricsTimer);
        liveMetricsTimer = null;
    }
}

// ─── RESTART SERVER & ROOM CLEANUP WORKFLOWS ─────────────────────────────────

function openRestartModal() {
    if (restartReason) restartReason.value = '';
    if (restartConfirmModal) restartConfirmModal.classList.add('show');
}

function closeRestartModal() {
    if (restartConfirmModal) restartConfirmModal.classList.remove('show');
}

async function handleConfirmRestart() {
    closeRestartModal();
    
    const reason = restartReason ? restartReason.value.trim() : '';
    
    // Hiển thị Overlay đếm ngược khởi động lại
    if (rebootOverlay) {
        rebootOverlay.style.display = 'flex';
        txtRebootTitle.textContent = 'ĐANG KHỞI ĐỘNG LẠI MÁY CHỦ';
        txtRebootSubtitle.textContent = 'Vui lòng đợi trong giây lát, hệ thống đang gửi tín hiệu và tự động kết nối lại...';
        
        step1.className = 'step-item active';
        step2.className = 'step-item';
        step3.className = 'step-item';
        step3Text.textContent = 'Đang kiểm tra trạng thái sức khỏe máy chủ (/health)...';
    }
    
    let elapsedSeconds = 0;
    if (rebootTimerVal) rebootTimerVal.textContent = '0s';
    const timerTicker = setInterval(() => {
        elapsedSeconds++;
        if (rebootTimerVal) rebootTimerVal.textContent = `${elapsedSeconds}s`;
    }, 1000);

    try {
        // Gửi yêu cầu khởi động lại Server
        const res = await fetch(`${API_URL}/api/admin/server/restart`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify({ reason: reason || 'Khởi động lại máy chủ từ Admin Dashboard' })
        });
        
        if (!res.ok) {
            const errData = await res.json().catch(() => ({}));
            throw new Error(errData.message || 'Không thể gửi lệnh khởi động lại.');
        }

        step1.className = 'step-item done';
        step2.className = 'step-item active';

        // Đợi 1.5 giây để tiến trình cũ kết thúc và supervisor nạp lại tiến trình mới
        await new Promise(r => setTimeout(r, 1500));
        
        step2.className = 'step-item done';
        step3.className = 'step-item active';

        // Bắt đầu Health-Check Polling kiểm tra máy chủ đã sẵn sàng chưa
        let reconnected = false;
        let attempts = 0;
        const maxAttempts = 30; // 30 giây tối đa

        while (!reconnected && attempts < maxAttempts) {
            attempts++;
            step3Text.textContent = `Đang kiểm tra kết nối lại máy chủ... (Lần ${attempts})`;
            await new Promise(r => setTimeout(r, 1000));
            
            try {
                const healthRes = await fetch(`${API_URL}/health?t=${Date.now()}`, {
                    cache: 'no-store'
                });
                if (healthRes.ok) {
                    const healthData = await healthRes.json();
                    if (healthData.status === 'ok') {
                        reconnected = true;
                        break;
                    }
                }
            } catch (e) {
                // Server đang khởi động lại, tiếp tục vòng lặp
            }
        }

        clearInterval(timerTicker);

        if (reconnected) {
            step3.className = 'step-item done';
            txtRebootTitle.textContent = 'KHỞI ĐỘNG LẠI THÀNH CÔNG!';
            txtRebootSubtitle.textContent = 'Máy chủ đã online và sẵn sàng phục vụ!';
            
            showToast('Máy chủ đã khởi động lại và kết nối thành công!', 'success');
            
            setTimeout(() => {
                if (rebootOverlay) rebootOverlay.style.display = 'none';
                fetchStats();
                fetchSystemMetrics();
            }, 1200);
        } else {
            txtRebootTitle.textContent = 'KẾT NỐI MẤT NHIỀU THỜI GIAN';
            txtRebootSubtitle.textContent = 'Máy chủ có thể đang mất nhiều thời gian hơn để khởi động lại. Vui lòng thử làm mới trang.';
            showToast('Không thể xác nhận server đã online sau 30s. Hãy thử tải lại trang.', 'warning');
            setTimeout(() => {
                if (rebootOverlay) rebootOverlay.style.display = 'none';
            }, 3000);
        }

    } catch (err) {
        clearInterval(timerTicker);
        if (rebootOverlay) rebootOverlay.style.display = 'none';
        console.error('[Restart Error]', err);
        showToast(`Lỗi khi khởi động lại máy chủ: ${err.message}`, 'error');
    }
}

async function handleCleanupRooms() {
    if (!confirm('Bạn có chắc chắn muốn dọn dẹp các phòng chơi trống không có người hoặc đã kết thúc không?')) {
        return;
    }

    try {
        const res = await fetch(`${API_URL}/api/admin/rooms/cleanup`, {
            method: 'POST',
            headers: getHeaders()
        });
        const data = await res.json();
        if (res.ok) {
            showToast(data.message || 'Dọn dẹp phòng rác thành công!', 'success');
            fetchStats();
            if (activeTab === 'tab-rooms') fetchRooms();
        } else {
            showToast(data.message || 'Lỗi khi dọn dẹp phòng.', 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Không thể thực hiện dọn dẹp phòng.', 'error');
    }
}

// ─── FORMATTING HELPERS ──────────────────────────────────────────────────────

function formatBytes(bytes) {
    if (bytes === 0 || !bytes) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
}

function formatUptime(seconds) {
    if (!seconds || seconds <= 0) return '0s';
    const d = Math.floor(seconds / (3600 * 24));
    const h = Math.floor((seconds % (3600 * 24)) / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    
    const parts = [];
    if (d > 0) parts.push(`${d}d`);
    if (h > 0) parts.push(`${h}h`);
    if (m > 0) parts.push(`${m}m`);
    if (s > 0 && d === 0) parts.push(`${s}s`);
    return parts.join(' ') || '0s';
}

// ─── UTILITIES ───────────────────────────────────────────────────────────────
function escapeHTML(str) {
    if (!str) return '';
    return str.replace(/[&<>'"]/g, 
        tag => ({
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            "'": '&#39;',
            '"': '&quot;'
        }[tag] || tag)
    );
}

// Attach functions to window
window.openEditModal = openEditModal;
window.deleteUser = deleteUser;
window.deleteRoom = deleteRoom;
window.resetAdminPassword = resetAdminPassword;
