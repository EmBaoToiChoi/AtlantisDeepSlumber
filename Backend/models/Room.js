const mongoose = require('mongoose');

const roomSchema = new mongoose.Schema({
    roomId: {
        type: String,
        required: true,
        unique: true,
        trim: true,
        minlength: 6,
        maxlength: 6
    },
    roomName: {
        type: String,
        required: true,
        trim: true
    },
    host: {
        type: mongoose.Schema.Types.ObjectId,
        ref: 'User',
        required: true
    },
    players: [{
        user: { type: mongoose.Schema.Types.ObjectId, ref: 'User' },
        displayName: String,
        slot: Number // 1, 2, 3, 4
    }],
    isPrivate: {
        type: Boolean,
        default: false
    },
    password: {
        type: String,
        default: null
    },
    maxPlayers: {
        type: Number,
        default: 4
    },
    status: {
        type: String,
        enum: ['waiting', 'playing', 'finished'],
        default: 'waiting'
    },
    createdAt: {
        type: Date,
        default: Date.now,
        expires: 3600 // Tự động xóa phòng sau 1 giờ nếu không có hoạt động (ttl)
    }
});

module.exports = mongoose.model('Room', roomSchema);
