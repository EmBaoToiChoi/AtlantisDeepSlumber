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

// Ping & Stability Benchmark Elements
const btnRunStabilityTest = document.getElementById('btnRunStabilityTest');
const txtLivePingVal = document.getElementById('txtLivePingVal');
const alarmLevelPill = document.getElementById('alarmLevelPill');
const alarmLevelText = document.getElementById('alarmLevelText');
const txtAvgPingVal = document.getElementById('txtAvgPingVal');
const txtJitterVal = document.getElementById('txtJitterVal');
const txtStabilityScore = document.getElementById('txtStabilityScore');
const txtStabilityRating = document.getElementById('txtStabilityRating');
const badgeGameQuality = document.getElementById('badgeGameQuality');
const txtGameQualityDesc = document.getElementById('txtGameQualityDesc');
const txtTestStatus = document.getElementById('txtTestStatus');
const pingBarsTrack = document.getElementById('pingBarsTrack');
let clientPingHistory = [];
let isTestingStability = false;

// Docker Game Server & Console Elements
const badgeDockerSysStatus = document.getElementById('badgeDockerSysStatus');
const btnSysOpenDeployModal = document.getElementById('btnSysOpenDeployModal');
const btnSysDockerRestart = document.getElementById('btnSysDockerRestart');
const btnSysViewGameLogs = document.getElementById('btnSysViewGameLogs');

const gamelogDockerStatusChip = document.getElementById('gamelogDockerStatusChip');
const txtGamelogContainerState = document.getElementById('txtGamelogContainerState');
const btnOpenDeployFromLogs = document.getElementById('btnOpenDeployFromLogs');
const btnDockerRestartFromLogs = document.getElementById('btnDockerRestartFromLogs');
const btnDockerTogglePower = document.getElementById('btnDockerTogglePower');
const txtDockerPowerLabel = document.getElementById('txtDockerPowerLabel');

const inputFilterGameLogs = document.getElementById('inputFilterGameLogs');
const toggleLiveGameLogs = document.getElementById('toggleLiveGameLogs');
const toggleAutoScrollLogs = document.getElementById('toggleAutoScrollLogs');
const btnRefreshGameLogs = document.getElementById('btnRefreshGameLogs');
const btnCopyGameLogs = document.getElementById('btnCopyGameLogs');
const btnClearTerminalScreen = document.getElementById('btnClearTerminalScreen');
const txtLogLineCount = document.getElementById('txtLogLineCount');
const gameTerminalViewport = document.getElementById('gameTerminalViewport');
const gameTerminalPre = document.getElementById('gameTerminalPre');

// Deploy Modal Elements
const dockerDeployModal = document.getElementById('dockerDeployModal');
const btnCloseDeployModal = document.getElementById('btnCloseDeployModal');
const btnCancelDeploy = document.getElementById('btnCancelDeploy');
const btnStartDeployProcess = document.getElementById('btnStartDeployProcess');
const depStep1 = document.getElementById('depStep1');
const depStep2 = document.getElementById('depStep2');
const depStep3 = document.getElementById('depStep3');
const depStep4 = document.getElementById('depStep4');
const depConn1 = document.getElementById('depConn1');
const depConn2 = document.getElementById('depConn2');
const depConn3 = document.getElementById('depConn3');
const dotDeployStatus = document.getElementById('dotDeployStatus');
const txtDeployStatus = document.getElementById('txtDeployStatus');
const txtDeployTimer = document.getElementById('txtDeployTimer');
const deployTerminalBody = document.getElementById('deployTerminalBody');
const deployTerminalPre = document.getElementById('deployTerminalPre');

// Build Upload Elements
const buildDropzone = document.getElementById('buildDropzone');
const inputBuildZipFile = document.getElementById('inputBuildZipFile');
const dropzoneContent = document.getElementById('dropzoneContent');
const dropzoneFileInfo = document.getElementById('dropzoneFileInfo');
const btnBrowseFile = document.getElementById('btnBrowseFile');
const txtSelectedFileName = document.getElementById('txtSelectedFileName');
const txtSelectedFileSize = document.getElementById('txtSelectedFileSize');
const btnRemoveSelectedFile = document.getElementById('btnRemoveSelectedFile');
const chkAutoDeployAfterUpload = document.getElementById('chkAutoDeployAfterUpload');
const btnStartUploadBuild = document.getElementById('btnStartUploadBuild');
const uploadProgressWrapper = document.getElementById('uploadProgressWrapper');
const txtUploadProgressStatus = document.getElementById('txtUploadProgressStatus');
const txtUploadSpeed = document.getElementById('txtUploadSpeed');
const uploadProgressFill = document.getElementById('uploadProgressFill');

let selectedBuildFile = null;
let isUploadingBuild = false;
let gameLogsTimer = null;
let deployPollingTimer = null;
let rawGameLogs = '';
let currentContainerStatus = 'not_found';
let deployTicker = null;
let deploySeconds = 0;

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
                showToast('Đã bật chế độ cập nhật Realtime (1s).', 'info');
            } else {
                stopLiveMetricsTimer();
                showToast('Đã tắt tự động cập nhật phần cứng.', 'info');
            }
        });
    }

    // Ping & Stability Benchmark Button
    if (btnRunStabilityTest) {
        btnRunStabilityTest.addEventListener('click', runStabilityBenchmark);
    }

    // Docker & Deploy Modal Buttons
    if (btnSysOpenDeployModal) btnSysOpenDeployModal.addEventListener('click', openDeployModal);
    if (btnOpenDeployFromLogs) btnOpenDeployFromLogs.addEventListener('click', openDeployModal);
    if (btnCloseDeployModal) btnCloseDeployModal.addEventListener('click', closeDeployModal);
    if (btnCancelDeploy) btnCancelDeploy.addEventListener('click', closeDeployModal);
    if (btnStartDeployProcess) btnStartDeployProcess.addEventListener('click', handleStartDeploy);

    if (btnSysDockerRestart) btnSysDockerRestart.addEventListener('click', () => handleDockerAction('restart'));
    if (btnDockerRestartFromLogs) btnDockerRestartFromLogs.addEventListener('click', () => handleDockerAction('restart'));
    if (btnDockerTogglePower) btnDockerTogglePower.addEventListener('click', handleDockerPowerToggle);
    if (btnSysViewGameLogs) btnSysViewGameLogs.addEventListener('click', () => switchTab('tab-gamelogs'));

    if (inputFilterGameLogs) inputFilterGameLogs.addEventListener('input', applyGameLogFilter);
    if (btnRefreshGameLogs) btnRefreshGameLogs.addEventListener('click', fetchGameLogs);
    if (btnCopyGameLogs) btnCopyGameLogs.addEventListener('click', handleCopyGameLogs);
    if (btnClearTerminalScreen) btnClearTerminalScreen.addEventListener('click', handleClearTerminal);

    if (toggleLiveGameLogs) {
        toggleLiveGameLogs.addEventListener('change', () => {
            if (toggleLiveGameLogs.checked) {
                startLiveGameLogsTimer();
                showToast('Đã bật theo dõi log game trực tiếp (2s).', 'info');
            } else {
                stopLiveGameLogsTimer();
                showToast('Đã tắt tự động làm mới log.', 'info');
            }
        });
    }

    // Build Upload Event Listeners
    if (buildDropzone && inputBuildZipFile) {
        buildDropzone.addEventListener('click', (e) => {
            if (e.target !== btnRemoveSelectedFile && !btnRemoveSelectedFile?.contains(e.target)) {
                inputBuildZipFile.click();
            }
        });

        inputBuildZipFile.addEventListener('change', (e) => {
            if (e.target.files && e.target.files.length > 0) {
                handleSelectBuildFile(e.target.files[0]);
            }
        });

        buildDropzone.addEventListener('dragover', (e) => {
            e.preventDefault();
            buildDropzone.classList.add('dragover');
        });

        buildDropzone.addEventListener('dragleave', () => {
            buildDropzone.classList.remove('dragover');
        });

        buildDropzone.addEventListener('drop', (e) => {
            e.preventDefault();
            buildDropzone.classList.remove('dragover');
            if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                handleSelectBuildFile(e.dataTransfer.files[0]);
            }
        });
    }

    if (btnRemoveSelectedFile) {
        btnRemoveSelectedFile.addEventListener('click', (e) => {
            e.stopPropagation();
            handleClearSelectedBuildFile();
        });
    }

    if (btnStartUploadBuild) {
        btnStartUploadBuild.addEventListener('click', uploadBuildFile);
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
    } else if (tabId === 'tab-gamelogs') {
        currentTabTitle.textContent = 'Logs Game Server & Quản Lý Docker';
        currentTabDesc.textContent = 'Xem terminal log in-game trực tiếp từ container live_server và điều khiển Rebuild Image 4 bước.';
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
        await Promise.all([fetchSystemMetrics(), fetchDockerStatus()]);
        if (!toggleLiveMetrics || toggleLiveMetrics.checked) {
            startLiveMetricsTimer();
        }
    } else if (tabId === 'tab-gamelogs') {
        stopLiveMetricsTimer();
        await Promise.all([fetchDockerStatus(), fetchGameLogs()]);
        if (!toggleLiveGameLogs || toggleLiveGameLogs.checked) {
            startLiveGameLogsTimer();
        }
    } else {
        stopLiveMetricsTimer();
        stopLiveGameLogsTimer();
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
        // Đồng thời đo Ping thực tế từ Client tới Server
        measureClientPing();

        const res = await fetch(`${API_URL}/api/admin/system-metrics`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            if (data.success && data.metrics) {
                updateSystemMetricsUI(data.metrics);
            }
        } else {
            const errData = await res.json().catch(() => ({}));
            console.warn('[SystemMetrics Error]', res.status, errData);
            if (res.status === 404) {
                console.error('API /api/admin/system-metrics chưa có trên Backend VPS. Vui lòng cập nhật (git pull/upload) file server.js mới lên VPS.');
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
    }, 1000);
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

// ─── CLIENT PING & STABILITY BENCHMARK ───────────────────────────────────────

async function measureClientPing() {
    if (isTestingStability) return;
    try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 3000);
        const start = performance.now();
        const res = await fetch(`${API_URL}/api/ping?clientTime=${Date.now()}&_t=${Date.now()}`, {
            cache: 'no-store',
            signal: controller.signal
        });
        clearTimeout(timeoutId);
        if (res.ok) {
            const latency = Math.max(1, Math.min(999, Math.round(performance.now() - start)));
            clientPingHistory.push(latency);
            if (clientPingHistory.length > 15) clientPingHistory.shift();

            updatePingUI(latency);
        }
    } catch (err) {
        if (err.name === 'AbortError') {
            updatePingUI(999);
        }
    }
}

function updatePingUI(latency) {
    if (!txtLivePingVal) return;

    txtLivePingVal.textContent = latency;

    // Calculate Average
    const sum = clientPingHistory.reduce((a, b) => a + b, 0);
    const avg = clientPingHistory.length > 0 ? Math.round(sum / clientPingHistory.length) : latency;
    if (txtAvgPingVal) txtAvgPingVal.textContent = avg;

    // Calculate Jitter
    let jitter = 0;
    if (clientPingHistory.length > 1) {
        let diffSum = 0;
        for (let i = 1; i < clientPingHistory.length; i++) {
            diffSum += Math.abs(clientPingHistory[i] - clientPingHistory[i - 1]);
        }
        jitter = Math.round((diffSum / (clientPingHistory.length - 1)) * 10) / 10;
    }
    if (txtJitterVal) txtJitterVal.textContent = `±${jitter} ms`;

    // Stability %
    const penalty = (jitter * 1.8) + (avg > 90 ? (avg - 90) * 0.25 : 0);
    const stability = Math.max(15, Math.min(100, Math.round(100 - penalty)));
    if (txtStabilityScore) txtStabilityScore.textContent = `${stability}%`;

    // Determine Alarm Level & Colors
    if (alarmLevelPill && alarmLevelText) {
        if (latency < 60) {
            alarmLevelPill.className = 'ping-alarm-pill level-good';
            alarmLevelText.textContent = '🟢 MỨC 1: HOÀN HẢO (< 60ms)';
            if (txtStabilityRating) txtStabilityRating.textContent = 'Rất ổn định (Phản hồi tức thì)';
            if (badgeGameQuality) {
                badgeGameQuality.className = 'badge badge-success';
                badgeGameQuality.textContent = 'ĐẠT CHUẨN THI ĐẤU';
            }
            if (txtGameQualityDesc) txtGameQualityDesc.textContent = 'Phản hồi tức thì, combo kỹ năng chuẩn xác không có độ trễ.';
        } else if (latency <= 120) {
            alarmLevelPill.className = 'ping-alarm-pill level-moderate';
            alarmLevelText.textContent = '🟡 MỨC 2: BÌNH THƯỜNG (60-120ms)';
            if (txtStabilityRating) txtStabilityRating.textContent = 'Ổn định (Chơi mượt)';
            if (badgeGameQuality) {
                badgeGameQuality.className = 'badge badge-warning';
                badgeGameQuality.textContent = 'KẾT NỐI ỔN ĐỊNH';
            }
            if (txtGameQualityDesc) txtGameQualityDesc.textContent = 'Chơi game bình thường, độ trễ vừa phải, trải nghiệm tốt.';
        } else if (latency <= 200) {
            alarmLevelPill.className = 'ping-alarm-pill level-warning';
            alarmLevelText.textContent = '🟠 MỨC 3: CẢNH BÁO (121-200ms)';
            if (txtStabilityRating) txtStabilityRating.textContent = 'Trung bình (Trễ mạng nhẹ)';
            if (badgeGameQuality) {
                badgeGameQuality.className = 'badge badge-warning';
                badgeGameQuality.textContent = 'CẢNH BÁO GIẬT LAG';
            }
            if (txtGameQualityDesc) txtGameQualityDesc.textContent = 'Có hiện tượng trễ mạng, tung chiêu có thể bị delay nhẹ.';
        } else {
            alarmLevelPill.className = 'ping-alarm-pill level-danger';
            alarmLevelText.textContent = '🔴 MỨC 4: BÁO ĐỘNG ĐỎ (> 200ms)';
            if (txtStabilityRating) txtStabilityRating.textContent = 'Kém / Không ổn định';
            if (badgeGameQuality) {
                badgeGameQuality.className = 'badge badge-danger';
                badgeGameQuality.textContent = 'LAG NẶNG / NGUY HIỂM';
            }
            if (txtGameQualityDesc) txtGameQualityDesc.textContent = 'Mạng rất kém! Khuyến nghị kiểm tra kết nối trước khi vào trận.';
        }
    }
}

async function runStabilityBenchmark() {
    if (isTestingStability) return;
    isTestingStability = true;

    if (btnRunStabilityTest) {
        btnRunStabilityTest.disabled = true;
        btnRunStabilityTest.innerHTML = '<span>Đang đo kiểm tra (10 lần)...</span>';
    }

    if (txtTestStatus) txtTestStatus.textContent = 'Đang tiến hành đo 10 gói tin ping liên tiếp...';

    const bars = pingBarsTrack ? pingBarsTrack.querySelectorAll('.ping-sample-bar') : [];
    bars.forEach(b => {
        b.className = 'ping-sample-bar';
        b.style.height = '10%';
        const tag = b.querySelector('.bar-tag');
        if (tag) tag.textContent = '...';
    });

    const results = [];
    for (let i = 0; i < 10; i++) {
        const bar = bars[i];
        if (bar) {
            bar.className = 'ping-sample-bar testing';
            bar.style.height = '40%';
        }

        const start = performance.now();
        let ping = 999;
        try {
            const res = await fetch(`${API_URL}/api/ping?t=${Date.now()}`, { cache: 'no-store' });
            if (res.ok) {
                ping = Math.max(1, Math.round(performance.now() - start));
            }
        } catch (e) {
            ping = 999;
        }

        results.push(ping);
        clientPingHistory.push(ping);
        if (clientPingHistory.length > 20) clientPingHistory.shift();

        // Update bar
        if (bar) {
            const heightPercent = Math.min(100, Math.max(15, Math.round((ping / 250) * 100)));
            bar.style.height = `${heightPercent}%`;
            const tag = bar.querySelector('.bar-tag');
            if (tag) tag.textContent = `${ping}ms`;

            if (ping < 60) bar.className = 'ping-sample-bar good';
            else if (ping <= 120) bar.className = 'ping-sample-bar moderate';
            else if (ping <= 200) bar.className = 'ping-sample-bar warning';
            else bar.className = 'ping-sample-bar danger';
        }

        updatePingUI(ping);
        await new Promise(r => setTimeout(r, 200));
    }

    // Benchmark summary
    const minPing = Math.min(...results);
    const maxPing = Math.max(...results);
    const avgPing = Math.round(results.reduce((a, b) => a + b, 0) / results.length);

    if (txtTestStatus) {
        txtTestStatus.textContent = `Hoàn tất! Min: ${minPing}ms | Max: ${maxPing}ms | Avg: ${avgPing}ms`;
    }

    if (btnRunStabilityTest) {
        btnRunStabilityTest.disabled = false;
        btnRunStabilityTest.innerHTML = `
            <svg viewBox="0 0 24 24" class="icon-btn">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 14.5v-9l6 4.5-6 4.5z"/>
            </svg>
            <span>Đo lại Độ Ổn Định</span>
        `;
    }

    isTestingStability = false;

    if (avgPing < 80) {
        showToast(`Kết quả kiểm tra xuất sắc! Ping TB: ${avgPing}ms (Đạt chuẩn thi đấu)`, 'success');
    } else if (avgPing <= 150) {
        showToast(`Kết quả: Ping TB ${avgPing}ms (Kết nối chơi game ổn định)`, 'info');
    } else {
        showToast(`Cảnh báo: Ping TB ${avgPing}ms (Mạng có hiện tượng lag trễ)`, 'error');
    }
}

// ─── DOCKER GAME SERVER & LIVE LOGS MANAGER ─────────────────────────────────

async function fetchDockerStatus() {
    if (!ADMIN_TOKEN) return;
    try {
        const res = await fetch(`${API_URL}/api/admin/docker/status`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            if (data.success) {
                updateDockerStatusUI(data.container, data.deployState);
            }
        }
    } catch (err) {
        console.error('[FetchDockerStatus Error]', err);
    }
}

function updateDockerStatusUI(container, deployState) {
    if (!container) return;
    currentContainerStatus = container.status || 'not_found';
    const isRunning = currentContainerStatus === 'running';

    // Update tab-system badge
    if (badgeDockerSysStatus) {
        if (isRunning) {
            badgeDockerSysStatus.className = 'badge badge-success';
            badgeDockerSysStatus.textContent = '🟢 DOCKER: RUNNING (Port 7777)';
        } else if (currentContainerStatus === 'exited') {
            badgeDockerSysStatus.className = 'badge badge-danger';
            badgeDockerSysStatus.textContent = '🔴 DOCKER: STOPPED';
        } else {
            badgeDockerSysStatus.className = 'badge badge-warning';
            badgeDockerSysStatus.textContent = '⚪ DOCKER: NOT FOUND';
        }
    }

    // Update tab-gamelogs status chip
    if (gamelogDockerStatusChip && txtGamelogContainerState) {
        const dot = gamelogDockerStatusChip.querySelector('.ping-dot');
        if (dot) {
            dot.style.background = isRunning ? 'var(--green)' : 'var(--danger)';
        }
        txtGamelogContainerState.textContent = `Container: live_server (${isRunning ? 'RUNNING' : 'STOPPED'})`;
    }

    // Update Power button label
    if (txtDockerPowerLabel) {
        txtDockerPowerLabel.textContent = isRunning ? 'Dừng Container' : 'Khởi động Container';
    }

    // If currently deploying, update modal stepper
    if (deployState && deployState.isDeploying) {
        updateDeployStepperUI(deployState);
        if (!deployPollingTimer) {
            pollDeployProgress();
        }
    }
}

async function fetchGameLogs() {
    if (!ADMIN_TOKEN) return;
    try {
        const res = await fetch(`${API_URL}/api/admin/docker/game-logs?lines=300`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            if (data.success) {
                rawGameLogs = data.logs || '';
                renderGameLogs();
            }
        }
    } catch (err) {
        console.error('[FetchGameLogs Error]', err);
    }
}

function renderGameLogs() {
    if (!gameTerminalPre) return;
    const filterQuery = inputFilterGameLogs ? inputFilterGameLogs.value.trim().toLowerCase() : '';
    
    const lines = rawGameLogs.split('\n');
    let filteredLines = lines;
    if (filterQuery) {
        filteredLines = lines.filter(l => l.toLowerCase().includes(filterQuery));
    }

    if (txtLogLineCount) {
        txtLogLineCount.textContent = `${filteredLines.length} / ${lines.length} dòng log`;
    }

    if (filteredLines.length === 0) {
        gameTerminalPre.innerHTML = filterQuery 
            ? `<span style="color: var(--text-muted);">Không tìm thấy dòng log nào khớp với từ khóa "${escapeHTML(filterQuery)}".</span>`
            : '<span style="color: var(--text-muted);">Chưa có log từ Game Server.</span>';
        return;
    }

    // Syntax highlighting for game logs
    const formattedHtml = filteredLines.map(line => {
        const escaped = escapeHTML(line);
        if (line.includes('[ERROR]') || line.includes('Exception') || line.includes('Error') || line.includes('Failed')) {
            return `<span style="color: #ff5f56; font-weight: bold;">${escaped}</span>`;
        } else if (line.includes('[WARN]') || line.includes('Warning') || line.includes('Disconnect')) {
            return `<span style="color: #ffbd2e;">${escaped}</span>`;
        } else if (line.includes('[SERVER]') || line.includes('Dedicated Server') || line.includes('Lobby')) {
            return `<span style="color: #00e8ff; font-weight: 600;">${escaped}</span>`;
        } else if (line.includes('[CLIENT]') || line.includes('Connected') || line.includes('Approved') || line.includes('Player')) {
            return `<span style="color: #00ffc4;">${escaped}</span>`;
        } else {
            return `<span style="color: #c9d8e8;">${escaped}</span>`;
        }
    }).join('\n');

    gameTerminalPre.innerHTML = formattedHtml;

    // Auto-scroll to bottom if enabled
    if (toggleAutoScrollLogs && toggleAutoScrollLogs.checked && gameTerminalViewport) {
        gameTerminalViewport.scrollTop = gameTerminalViewport.scrollHeight;
    }
}

function applyGameLogFilter() {
    renderGameLogs();
}

function handleCopyGameLogs() {
    if (!rawGameLogs) {
        showToast('Không có dữ liệu log để sao chép.', 'warning');
        return;
    }
    navigator.clipboard.writeText(rawGameLogs)
        .then(() => showToast('Đã sao chép toàn bộ log game vào Clipboard!', 'success'))
        .catch(() => showToast('Không thể sao chép log.', 'error'));
}

function handleClearTerminal() {
    if (gameTerminalPre) {
        gameTerminalPre.innerHTML = '<span style="color: var(--text-muted);">Màn hình đã được xóa. Bấm "Làm mới" để tải lại log.</span>';
    }
    if (txtLogLineCount) {
        txtLogLineCount.textContent = '0 dòng log';
    }
}

async function handleDockerAction(action) {
    const actionName = action === 'restart' ? 'Khởi động lại' : action === 'stop' ? 'Dừng' : 'Bật';
    if (!confirm(`Bạn có chắc chắn muốn ${actionName} Container live_server không?`)) {
        return;
    }

    try {
        showToast(`Đang gửi lệnh ${actionName} container...`, 'info');
        const res = await fetch(`${API_URL}/api/admin/docker/action`, {
            method: 'POST',
            headers: getHeaders(),
            body: JSON.stringify({ action })
        });
        const data = await res.json();
        if (res.ok) {
            showToast(data.message || `${actionName} container thành công!`, 'success');
            setTimeout(() => {
                fetchDockerStatus();
                fetchGameLogs();
            }, 1000);
        } else {
            showToast(data.message || `Lỗi khi ${actionName} container.`, 'error');
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi mạng khi điều khiển container.', 'error');
    }
}

function handleDockerPowerToggle() {
    if (currentContainerStatus === 'running') {
        handleDockerAction('stop');
    } else {
        handleDockerAction('start');
    }
}

// ─── DEPLOY MODAL & REBUILD AUTOMATION ───────────────────────────────────────

function openDeployModal() {
    if (dockerDeployModal) {
        dockerDeployModal.classList.add('show');
        fetchDeployLogs();
    }
}

function closeDeployModal() {
    if (dockerDeployModal) {
        dockerDeployModal.classList.remove('show');
    }
}

async function handleStartDeploy() {
    if (!confirm('Xác nhận bắt đầu Rebuild Image (vps_server) và Deploy lại Game Server (live_server)?')) {
        return;
    }

    if (btnStartDeployProcess) {
        btnStartDeployProcess.disabled = true;
        btnStartDeployProcess.innerHTML = '<span>Đang khởi động tiến trình...</span>';
    }

    deploySeconds = 0;
    if (txtDeployTimer) txtDeployTimer.textContent = 'Thời gian: 0s';
    clearInterval(deployTicker);
    deployTicker = setInterval(() => {
        deploySeconds++;
        if (txtDeployTimer) txtDeployTimer.textContent = `Thời gian: ${deploySeconds}s`;
    }, 1000);

    try {
        const res = await fetch(`${API_URL}/api/admin/docker/deploy`, {
            method: 'POST',
            headers: getHeaders()
        });
        const data = await res.json();
        if (res.ok) {
            showToast('Đã bắt đầu tiến trình Rebuild & Deploy Docker!', 'info');
            pollDeployProgress();
        } else {
            showToast(data.message || 'Không thể bắt đầu Deploy.', 'error');
            if (btnStartDeployProcess) {
                btnStartDeployProcess.disabled = false;
                btnStartDeployProcess.innerHTML = `
                    <svg viewBox="0 0 24 24" class="icon-btn">
                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 14.5v-9l6 4.5-6 4.5z"/>
                    </svg>
                    <span>BẮT ĐẦU REBUILD & DEPLOY</span>
                `;
            }
        }
    } catch (err) {
        console.error(err);
        showToast('Lỗi mạng khi bắt đầu Deploy.', 'error');
        if (btnStartDeployProcess) {
            btnStartDeployProcess.disabled = false;
        }
    }
}

function pollDeployProgress() {
    if (deployPollingTimer) clearInterval(deployPollingTimer);
    
    deployPollingTimer = setInterval(async () => {
        const state = await fetchDeployLogs();
        if (state && !state.isDeploying) {
            clearInterval(deployPollingTimer);
            deployPollingTimer = null;
            clearInterval(deployTicker);
            
            if (state.status === 'success') {
                showToast('Rebuild & Thay Image Game Server thành công rực rỡ!', 'success');
            } else if (state.status === 'error') {
                showToast(`Deploy thất bại: ${state.error || 'Xem chi tiết trong log'}`, 'error');
            }

            if (btnStartDeployProcess) {
                btnStartDeployProcess.disabled = false;
                btnStartDeployProcess.innerHTML = `
                    <svg viewBox="0 0 24 24" class="icon-btn">
                        <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 14.5v-9l6 4.5-6 4.5z"/>
                    </svg>
                    <span>REBUILD & DEPLOY LẠI</span>
                `;
            }

            fetchDockerStatus();
            fetchGameLogs();
        }
    }, 1000);
}

async function fetchDeployLogs() {
    if (!ADMIN_TOKEN) return null;
    try {
        const res = await fetch(`${API_URL}/api/admin/docker/deploy-logs`, {
            headers: getHeaders()
        });
        if (res.ok) {
            const data = await res.json();
            if (data.success && data.deployState) {
                updateDeployStepperUI(data.deployState);
                return data.deployState;
            }
        }
    } catch (err) {
        console.error('[FetchDeployLogs Error]', err);
    }
    return null;
}

function updateDeployStepperUI(state) {
    if (!state) return;
    const { currentStep, isDeploying, status, stepName, logs, error } = state;

    // Update Status Badge
    if (txtDeployStatus && dotDeployStatus) {
        if (isDeploying) {
            txtDeployStatus.textContent = `Đang chạy: ${stepName}`;
            dotDeployStatus.style.background = 'var(--cyan)';
        } else if (status === 'success') {
            txtDeployStatus.textContent = 'Hoàn tất thành công (Container Online)';
            dotDeployStatus.style.background = 'var(--green)';
        } else if (status === 'error') {
            txtDeployStatus.textContent = `Lỗi: ${error || 'Thất bại'}`;
            dotDeployStatus.style.background = 'var(--danger)';
        } else {
            txtDeployStatus.textContent = 'Sẵn sàng thực thi';
            dotDeployStatus.style.background = 'var(--warning)';
        }
    }

    // Update Stepper steps
    const steps = [depStep1, depStep2, depStep3, depStep4];
    const connectors = [depConn1, depConn2, depConn3];

    steps.forEach((s, idx) => {
        if (!s) return;
        const stepNum = idx + 1;
        s.className = 'deploy-step-item';
        if (stepNum < currentStep || (status === 'success' && stepNum <= 4)) {
            s.classList.add('done');
        } else if (stepNum === currentStep) {
            if (status === 'error') s.classList.add('error');
            else if (isDeploying) s.classList.add('active');
        }
    });

    connectors.forEach((c, idx) => {
        if (!c) return;
        if (idx + 1 < currentStep || status === 'success') {
            c.className = 'step-connector done';
        } else {
            c.className = 'step-connector';
        }
    });

    // Update Terminal Logs
    if (deployTerminalPre && logs) {
        deployTerminalPre.textContent = logs.join('\n') || 'Đang chuẩn bị chạy lệnh...';
        if (deployTerminalBody) {
            deployTerminalBody.scrollTop = deployTerminalBody.scrollHeight;
        }
    }
}

function startLiveGameLogsTimer() {
    stopLiveGameLogsTimer();
    gameLogsTimer = setInterval(() => {
        if (activeTab === 'tab-gamelogs') {
            fetchGameLogs();
            fetchDockerStatus();
        }
    }, 2000);
}

function stopLiveGameLogsTimer() {
    if (gameLogsTimer) {
        clearInterval(gameLogsTimer);
        gameLogsTimer = null;
    }
}

// ─── BUILD UPLOAD & AUTO DEPLOY HANDLERS ────────────────────────────────────

function handleSelectBuildFile(file) {
    if (!file) return;
    const ext = file.name.split('.').pop().toLowerCase();
    if (!['zip', 'rar', '7z', 'gz', 'tar', 'tgz'].includes(ext)) {
        showToast('Vui lòng chọn file nén bản build định dạng WinRAR (.rar), 7-Zip (.7z) hoặc .zip', 'warning');
        return;
    }

    selectedBuildFile = file;
    const sizeMB = (file.size / (1024 * 1024)).toFixed(2);

    if (txtSelectedFileName) txtSelectedFileName.textContent = file.name;
    if (txtSelectedFileSize) txtSelectedFileSize.textContent = `${sizeMB} MB`;

    if (dropzoneContent) dropzoneContent.style.display = 'none';
    if (dropzoneFileInfo) dropzoneFileInfo.style.display = 'flex';
    if (btnStartUploadBuild) btnStartUploadBuild.disabled = false;

    showToast(`Đã chọn file build: ${file.name} (${sizeMB} MB)`, 'info');
}

function handleClearSelectedBuildFile() {
    selectedBuildFile = null;
    if (inputBuildZipFile) inputBuildZipFile.value = '';
    if (dropzoneContent) dropzoneContent.style.display = 'flex';
    if (dropzoneFileInfo) dropzoneFileInfo.style.display = 'none';
    if (btnStartUploadBuild) btnStartUploadBuild.disabled = true;
    if (uploadProgressWrapper) uploadProgressWrapper.style.display = 'none';
}

function uploadBuildFile() {
    if (!selectedBuildFile) {
        showToast('Vui lòng chọn file build trước khi tải lên.', 'warning');
        return;
    }

    if (isUploadingBuild) return;
    isUploadingBuild = true;

    if (btnStartUploadBuild) {
        btnStartUploadBuild.disabled = true;
        btnStartUploadBuild.innerHTML = '<span>Đang tải lên...</span>';
    }

    if (uploadProgressWrapper) uploadProgressWrapper.style.display = 'block';
    if (uploadProgressFill) uploadProgressFill.style.width = '0%';
    if (txtUploadProgressStatus) txtUploadProgressStatus.textContent = 'Bắt đầu truyền file lên VPS: 0%';
    if (txtUploadSpeed) txtUploadSpeed.textContent = '0 MB/s';

    const autoDeploy = chkAutoDeployAfterUpload ? chkAutoDeployAfterUpload.checked : true;
    const startTime = Date.now();
    let lastLoaded = 0;
    let lastTime = startTime;

    const xhr = new XMLHttpRequest();
    xhr.open('POST', `${API_URL}/api/admin/docker/upload-build?autoDeploy=${autoDeploy}`, true);
    xhr.setRequestHeader('Authorization', `Bearer ${ADMIN_TOKEN}`);
    xhr.setRequestHeader('x-file-name', encodeURIComponent(selectedBuildFile.name));
    xhr.setRequestHeader('x-auto-deploy', autoDeploy ? 'true' : 'false');
    xhr.setRequestHeader('Content-Type', 'application/octet-stream');

    xhr.upload.onprogress = (e) => {
        if (e.lengthComputable) {
            const percent = Math.round((e.loaded / e.total) * 100);
            if (uploadProgressFill) uploadProgressFill.style.width = `${percent}%`;

            const now = Date.now();
            const timeDelta = (now - lastTime) / 1000;
            if (timeDelta >= 0.5) {
                const speedBps = (e.loaded - lastLoaded) / timeDelta;
                const speedMBps = (speedBps / (1024 * 1024)).toFixed(2);
                if (txtUploadSpeed) txtUploadSpeed.textContent = `${speedMBps} MB/s`;
                lastLoaded = e.loaded;
                lastTime = now;
            }

            if (txtUploadProgressStatus) {
                txtUploadProgressStatus.textContent = percent === 100 
                    ? 'Đang giải nén file trên VPS...' 
                    : `Đang tải lên VPS: ${percent}% (${(e.loaded / (1024 * 1024)).toFixed(1)} / ${(e.total / (1024 * 1024)).toFixed(1)} MB)`;
            }
        }
    };

    xhr.onload = () => {
        isUploadingBuild = false;
        if (btnStartUploadBuild) {
            btnStartUploadBuild.disabled = false;
            btnStartUploadBuild.innerHTML = `
                <svg viewBox="0 0 24 24" class="icon-btn">
                    <path d="M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z"/>
                </svg>
                <span>Tải Lên & Cập Nhật Server Ngay</span>
            `;
        }

        try {
            const data = JSON.parse(xhr.responseText);
            if (xhr.status === 200 && data.success) {
                showToast(data.message || 'Tải lên và giải nén thành công!', 'success');
                if (autoDeploy) {
                    openDeployModal();
                    pollDeployProgress();
                }
                handleClearSelectedBuildFile();
            } else {
                showToast(data.message || 'Lỗi khi tải lên file build.', 'error');
            }
        } catch (e) {
            showToast('Lỗi xử lý phản hồi từ máy chủ.', 'error');
        }
    };

    xhr.onerror = () => {
        isUploadingBuild = false;
        if (btnStartUploadBuild) {
            btnStartUploadBuild.disabled = false;
            btnStartUploadBuild.innerHTML = '<span>Thử tải lên lại</span>';
        }
        showToast('Lỗi kết nối khi truyền file lên VPS.', 'error');
    };

    xhr.send(selectedBuildFile);
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
