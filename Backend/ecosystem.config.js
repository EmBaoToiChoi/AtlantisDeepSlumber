module.exports = {
    apps: [
        {
            name: 'atlantis-backend',
            script: 'server.js',
            instances: 1,
            autorestart: true,
            watch: false,
            max_memory_restart: '256M',
            env: {
                NODE_ENV: 'production'
            },
            // Log files
            out_file: '/var/log/atlantis/out.log',
            error_file: '/var/log/atlantis/error.log',
            log_date_format: 'YYYY-MM-DD HH:mm:ss Z'
        }
    ]
};
