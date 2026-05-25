const mongoose = require('mongoose');

const userSchema = new mongoose.Schema({
    displayName: {
        type: String,
        required: true,
        trim: true,
        minlength: 2,
        maxlength: 32
    },
    email: {
        type: String,
        required: true,
        unique: true,
        lowercase: true,
        trim: true
    },
    passwordHash: {
        type: String,
        required: true
    },
    isVerified: {
        type: Boolean,
        default: false
    },
    // OTP lưu dưới dạng plain text (ngắn hạn, không cần hash)
    otp: {
        type: String,
        default: null
    },
    otpExpiry: {
        type: Date,
        default: null
    },
    otpAttempts: {
        type: Number,
        default: 0
    },
    playerState: {
        health: { type: Number, default: 100 },
        activeWeaponIndex: { type: Number, default: 1 },
        isWeapon2Locked: { type: Boolean, default: true },
        isSkillsUnlocked: { type: Boolean, default: false },
        inventorySlots: { type: [String], default: ["", "", "", "", "", "", "", "", "", ""] },
        // Upgrade System Stats
        upgradePoints: { type: Number, default: 5 },
        hpLevel: { type: Number, default: 0 },
        mpLevel: { type: Number, default: 0 },
        cooldownLevel: { type: Number, default: 0 },
        damageLevel: { type: Number, default: 0 }
    },
    createdAt: {
        type: Date,
        default: Date.now
    }
});

module.exports = mongoose.model('User', userSchema);
