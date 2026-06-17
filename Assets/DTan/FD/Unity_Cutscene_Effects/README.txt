
UNITY TIMELINE CUTSCENE EFFECTS

1. ScreenFade.cs
- Tạo Image fullscreen màu đen.
- Gắn script vào GameObject.
- Kéo Image vào fadeImage.
- Trong Timeline Animation Track, animate alpha từ 0 -> 1 hoặc 1 -> 0.

2. GlitchEffect.cs
- Tạo RawImage fullscreen với texture noise.
- Gắn script và animate SetIntensity() bằng Signal hoặc Animation Track.

3. CutsceneTransition.cs
- Dùng CanvasGroup để tạo chuyển cảnh đơn giản.
- Animate alpha trong Timeline.

Gợi ý:
- Import một texture noise trắng đen để tạo hiệu ứng nhiễu sóng.
- Có thể kết hợp Fade + Glitch để tạo hiệu ứng TV mất tín hiệu.
