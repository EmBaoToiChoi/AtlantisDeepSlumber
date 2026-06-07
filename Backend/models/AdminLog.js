const mongoose = require('mongoose');

const adminLogSchema = new mongoose.Schema({
    adminUsername: {
        type: String,
        required: true
    },
    adminDisplayName: {
        type: String,
        required: true
    },
    action: {
        type: String,
        required: true
    },
    target: {
        type: String,
        required: true
    },
    details: {
        type: String,
        default: ''
    },
    createdAt: {
        type: Date,
        default: Date.now
    }
});

module.exports = mongoose.model('AdminLog', adminLogSchema);
