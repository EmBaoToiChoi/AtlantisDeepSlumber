const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 5000;

const MIME_TYPES = {
    '.html': 'text/html',
    '.css': 'text/css',
    '.js': 'text/javascript',
    '.json': 'application/json',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.gif': 'image/gif',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon'
};

const server = http.createServer((req, res) => {
    console.log(`[Static Server] Request: ${req.method} ${req.url}`);

    // CORS Headers just in case
    res.setHeader('Access-Control-Allow-Origin', '*');

    // Chuyển hướng URL gốc hoặc không có file về index.html
    let filePath = req.url === '/' ? '/index.html' : req.url;
    
    // Tạo đường dẫn tuyệt đối đến tệp
    let fullPath = path.join(__dirname, filePath);
    
    // Đảm bảo không truy cập ra ngoài thư mục Frontend (bảo mật cơ bản)
    if (!fullPath.startsWith(__dirname)) {
        res.statusCode = 403;
        res.setHeader('Content-Type', 'text/plain; charset=utf-8');
        res.end('403 Forbidden - Truy cập bị từ chối');
        return;
    }

    // Kiểm tra tệp tồn tại
    fs.stat(fullPath, (err, stats) => {
        if (err || !stats.isFile()) {
            // Nếu không tìm thấy, fallback về index.html cho ứng dụng SPA
            fullPath = path.join(__dirname, 'index.html');
        }

        fs.readFile(fullPath, (err, data) => {
            if (err) {
                res.statusCode = 500;
                res.setHeader('Content-Type', 'text/plain; charset=utf-8');
                res.end('500 Internal Server Error - Lỗi đọc tệp');
                return;
            }

            const ext = path.extname(fullPath).toLowerCase();
            const contentType = MIME_TYPES[ext] || 'application/octet-stream';

            res.statusCode = 200;
            res.setHeader('Content-Type', contentType);
            res.end(data);
        });
    });
});

server.listen(PORT, () => {
    console.log(`\x1b[36m%s\x1b[0m`, `[Frontend] Admin Dashboard is running at: http://localhost:${PORT}`);
});
