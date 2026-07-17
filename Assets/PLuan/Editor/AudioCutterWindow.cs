using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

public class AudioCutterWindow : EditorWindow
{
    private AudioClip sourceClip;
    private float startTime = 0f;
    private float endTime = 0f;
    private string outputFileName = "";
    private string outputFolder = "";
    
    // Playback Preview variables
    private bool isPlayingPreview = false;
    private double previewStartTime = 0;
    private float previewDuration = 0f;

    // Time input variables (Phút:Giây)
    private string startTimeStr = "0:00.000";
    private string endTimeStr = "0:00.000";
    private AudioClip lastClip = null;

    [MenuItem("Window/PLuan/Audio Cutter")]
    public static void ShowWindow()
    {
        AudioCutterWindow window = GetWindow<AudioCutterWindow>("Audio Cutter");
        window.minSize = new Vector2(400, 350);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += UpdatePreviewState;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdatePreviewState;
        StopPreview();
    }

    private void OnGUI()
    {
        // Sync clip changes and formats
        if (sourceClip != lastClip)
        {
            lastClip = sourceClip;
            if (sourceClip != null)
            {
                startTime = 0f;
                endTime = sourceClip.length;
                startTimeStr = FormatTime(startTime);
                endTimeStr = FormatTime(endTime);
                
                string assetPath = AssetDatabase.GetAssetPath(sourceClip);
                outputFileName = Path.GetFileNameWithoutExtension(assetPath) + "_cut";
                outputFolder = Path.GetDirectoryName(assetPath);
            }
            else
            {
                startTime = 0f;
                endTime = 0f;
                startTimeStr = "0:00.000";
                endTimeStr = "0:00.000";
                outputFileName = "";
                outputFolder = "";
            }
        }

        // Title styling
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            margin = new RectOffset(10, 10, 15, 15)
        };
        
        EditorGUILayout.LabelField("PLuan Audio Cutter", titleStyle);
        EditorGUILayout.Space();

        // 1. File input section
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("1. Chọn File Âm Thanh Gốc", EditorStyles.boldLabel);
        
        sourceClip = (AudioClip)EditorGUILayout.ObjectField("Clip Âm Thanh", sourceClip, typeof(AudioClip), false);
        
        if (sourceClip != null)
        {
            EditorGUILayout.LabelField($"Độ dài gốc: {sourceClip.length:F3} giây ({FormatTime(sourceClip.length)})");
            EditorGUILayout.LabelField($"Tần số: {sourceClip.frequency} Hz | Số kênh: {sourceClip.channels}");
        }
        else
        {
            EditorGUILayout.HelpBox("Kéo và thả file âm thanh (MP3, WAV, OGG, v.v.) vào đây.", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        if (sourceClip == null)
        {
            return;
        }

        // 2. Select range
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("2. Chọn Khung Thời Gian Cần Cắt (Start & End)", EditorStyles.boldLabel);

        float maxDuration = sourceClip.length;
        
        // Double slider for selecting range
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.MinMaxSlider(ref startTime, ref endTime, 0f, maxDuration);
        if (EditorGUI.EndChangeCheck()) // If the slider moved
        {
            startTimeStr = FormatTime(startTime);
            endTimeStr = FormatTime(endTime);
        }
        
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Nhập thời gian dạng số giây (vd: 75.5) hoặc Phút:Giây (vd: 1:15.5):");
        
        // Input fields for Start and End
        EditorGUILayout.BeginHorizontal();
        
        // Start Time
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Thời gian Bắt đầu (Start):");
        EditorGUI.BeginChangeCheck();
        startTimeStr = EditorGUILayout.TextField(startTimeStr);
        if (EditorGUI.EndChangeCheck())
        {
            if (TryParseTime(startTimeStr, out float parsedStart))
            {
                startTime = Mathf.Clamp(parsedStart, 0f, endTime);
            }
        }
        EditorGUILayout.LabelField($"(= {startTime:F3} giây)", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        // End Time
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("Thời gian Kết thúc (End):");
        EditorGUI.BeginChangeCheck();
        endTimeStr = EditorGUILayout.TextField(endTimeStr);
        if (EditorGUI.EndChangeCheck())
        {
            if (TryParseTime(endTimeStr, out float parsedEnd))
            {
                endTime = Mathf.Clamp(parsedEnd, startTime, maxDuration);
            }
        }
        EditorGUILayout.LabelField($"(= {endTime:F3} giây)", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        
        float cutDuration = endTime - startTime;
        EditorGUILayout.LabelField("Thông tin chi tiết đoạn cắt:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"- Vị trí: {FormatTime(startTime)} -> {FormatTime(endTime)}");
        EditorGUILayout.LabelField($"- Độ dài file mới: {cutDuration:F3} giây ({FormatTime(cutDuration)})");
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // 3. Playback Preview
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("3. Nghe Thử Đoạn Đã Chọn", EditorStyles.boldLabel);
        
        if (isPlayingPreview)
        {
            double elapsed = EditorApplication.timeSinceStartup - previewStartTime;
            float currentPos = startTime + (float)elapsed;
            
            // Format playhead status
            string progressStr = $"{FormatTime(currentPos)} / {FormatTime(endTime)}";
            EditorGUILayout.LabelField($"Trạng thái: Đang phát... {progressStr}", EditorStyles.boldLabel);
            
            // Draw progress bar
            float progressPercentage = Mathf.InverseLerp(startTime, endTime, currentPos);
            Rect r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r, progressPercentage, $"{currentPos:F1}s / {endTime:F1}s");
        }
        else
        {
            EditorGUILayout.LabelField("Trạng thái: Đang dừng.");
            
            // Draw empty progress bar
            Rect r = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.ProgressBar(r, 0f, $"{startTime:F1}s / {endTime:F1}s");
        }
        
        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();

        if (isPlayingPreview)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Stop Preview", GUILayout.Height(30)))
            {
                StopPreview();
            }
        }
        else
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Play Preview", GUILayout.Height(30)))
            {
                PlayPreview();
            }
        }
        GUI.backgroundColor = Color.white; // Reset background color

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // 4. Output configuration
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("4. Lưu File Mới", EditorStyles.boldLabel);
        
        outputFileName = EditorGUILayout.TextField("Tên file mới:", outputFileName);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextField("Thư mục lưu:", outputFolder);
        if (GUILayout.Button("Chọn...", GUILayout.Width(60)))
        {
            string absoluteFolder = EditorUtility.OpenFolderPanel("Chọn thư mục lưu file", Application.dataPath, "");
            if (!string.IsNullOrEmpty(absoluteFolder))
            {
                // Convert absolute path to relative unity project path
                if (absoluteFolder.StartsWith(Application.dataPath))
                {
                    outputFolder = "Assets" + absoluteFolder.Substring(Application.dataPath.Length);
                }
                else
                {
                    EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn thư mục nằm bên trong thư mục Assets của dự án!", "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        // 5. Run button
        GUI.backgroundColor = new Color(0.2f, 0.6f, 1f);
        if (GUILayout.Button("CẮT VÀ LƯU FILE MỚI", GUILayout.Height(40)))
        {
            CutAndSaveAudio();
        }
        GUI.backgroundColor = Color.white;
    }

    private void UpdatePreviewState()
    {
        if (isPlayingPreview)
        {
            if (IsPreviewPlaying())
            {
                double elapsed = EditorApplication.timeSinceStartup - previewStartTime;
                if (elapsed >= previewDuration)
                {
                    StopPreview();
                }
                Repaint(); // Force UI update every frame to animate the progress bar
            }
            else
            {
                isPlayingPreview = false;
                Repaint();
            }
        }
    }

    private void PlayPreview()
    {
        if (sourceClip == null) return;

        StopPreview();

        int startSample = Mathf.FloorToInt(startTime * sourceClip.frequency);
        previewDuration = endTime - startTime;
        previewStartTime = EditorApplication.timeSinceStartup;
        
        PlayClipUsingReflection(sourceClip, startSample);
        isPlayingPreview = true;
    }

    private void StopPreview()
    {
        if (sourceClip != null)
        {
            StopClipUsingReflection(sourceClip);
        }
        isPlayingPreview = false;
    }

    private void PlayClipUsingReflection(AudioClip clip, int startSample)
    {
        try
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            System.Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                // Try PlayPreviewClip first (Unity 2020.2+)
                MethodInfo playPreviewMethod = audioUtilClass.GetMethod("PlayPreviewClip", new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) });
                if (playPreviewMethod != null)
                {
                    playPreviewMethod.Invoke(null, new object[] { clip, startSample, false });
                }
                else
                {
                    // Try PlayClip (older)
                    MethodInfo playMethod = audioUtilClass.GetMethod("PlayClip", new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) });
                    if (playMethod != null)
                    {
                        playMethod.Invoke(null, new object[] { clip, startSample, false });
                    }
                    else
                    {
                        // Fallback PlayClip with 2 args
                        playMethod = audioUtilClass.GetMethod("PlayClip", new System.Type[] { typeof(AudioClip), typeof(int) });
                        if (playMethod != null)
                        {
                            playMethod.Invoke(null, new object[] { clip, startSample });
                        }
                    }
                }

                // Call SetPreviewClipSamplePosition / SetClipSamplePosition to force starting at correct sample
                MethodInfo setPosMethod = audioUtilClass.GetMethod("SetPreviewClipSamplePosition", new System.Type[] { typeof(AudioClip), typeof(int) });
                if (setPosMethod != null)
                {
                    setPosMethod.Invoke(null, new object[] { clip, startSample });
                }
                else
                {
                    setPosMethod = audioUtilClass.GetMethod("SetClipSamplePosition", new System.Type[] { typeof(AudioClip), typeof(int) });
                    if (setPosMethod != null)
                    {
                        setPosMethod.Invoke(null, new object[] { clip, startSample });
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Không thể tự động phát nghe thử: " + e.Message);
        }
    }

    private void StopClipUsingReflection(AudioClip clip)
    {
        try
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            System.Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                // Try StopAllPreviewClips (Unity 2020.2+)
                MethodInfo method = audioUtilClass.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                {
                    method.Invoke(null, null);
                    return;
                }

                // Try StopClip (older)
                method = audioUtilClass.GetMethod("StopClip", new System.Type[] { typeof(AudioClip) });
                if (method != null)
                {
                    method.Invoke(null, new object[] { clip });
                    return;
                }

                // Try StopAllClips
                method = audioUtilClass.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                {
                    method.Invoke(null, null);
                }
            }
        }
        catch { }
    }

    private bool IsPreviewPlaying()
    {
        try
        {
            Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
            System.Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                // Try IsPreviewClipPlaying (Unity 2020.2+)
                MethodInfo method = audioUtilClass.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public);
                if (method != null)
                {
                    return (bool)method.Invoke(null, null);
                }

                // Try IsClipPlaying (older)
                method = audioUtilClass.GetMethod("IsClipPlaying", new System.Type[] { typeof(AudioClip) });
                if (method != null)
                {
                    return (bool)method.Invoke(null, new object[] { sourceClip });
                }
            }
        }
        catch { }
        return false;
    }

    private void CutAndSaveAudio()
    {
        if (sourceClip == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn một file âm thanh trước khi cắt!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(outputFileName))
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng nhập tên cho file mới!", "OK");
            return;
        }

        if (string.IsNullOrEmpty(outputFolder))
        {
            EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn thư mục lưu file mới!", "OK");
            return;
        }

        string destPath = Path.Combine(outputFolder, outputFileName + ".wav").Replace("\\", "/");
        
        // Prevent accidental overwriting of the original file
        string originalAssetPath = AssetDatabase.GetAssetPath(sourceClip).Replace("\\", "/");
        if (destPath.ToLower() == originalAssetPath.ToLower())
        {
            EditorUtility.DisplayDialog("Cảnh báo bảo vệ", "Bạn không thể đặt tên trùng khớp hoàn toàn với file gốc để tránh mất file gốc. Vui lòng chọn một tên khác!", "OK");
            return;
        }

        // Confirm overwrite if file already exists
        if (File.Exists(destPath))
        {
            if (!EditorUtility.DisplayDialog("Xác nhận", $"File '{outputFileName}.wav' đã tồn tại. Bạn có muốn ghi đè lên nó không?", "Có", "Không"))
            {
                return;
            }
        }

        bool wasStreaming = false;
        string assetPath = AssetDatabase.GetAssetPath(sourceClip);
        AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;

        // Streaming audio clip data cannot be read via GetData, we must temporarily change it to DecompressOnLoad
        if (importer != null && importer.defaultSampleSettings.loadType == AudioClipLoadType.Streaming)
        {
            wasStreaming = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = settings;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        EditorUtility.DisplayProgressBar("Đang cắt âm thanh", "Đang tải dữ liệu...", 0.2f);

        try
        {
            int channels = sourceClip.channels;
            int frequency = sourceClip.frequency;
            
            int startFrame = Mathf.FloorToInt(startTime * frequency);
            int endFrame = Mathf.FloorToInt(endTime * frequency);
            
            startFrame = Mathf.Clamp(startFrame, 0, sourceClip.samples);
            endFrame = Mathf.Clamp(endFrame, startFrame, sourceClip.samples);
            
            int lengthFrames = endFrame - startFrame;
            
            if (lengthFrames <= 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Lỗi", "Độ dài đoạn cắt phải lớn hơn 0 giây!", "OK");
                return;
            }

            float[] cutSamples = new float[lengthFrames * channels];
            sourceClip.GetData(cutSamples, startFrame);

            EditorUtility.DisplayProgressBar("Đang cắt âm thanh", "Đang ghi file WAV mới...", 0.6f);

            bool success = SaveWavFile(destPath, cutSamples, channels, frequency);
            
            EditorUtility.ClearProgressBar();

            if (success)
            {
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Thành công", $"Đã cắt và lưu thành công file mới tại:\n{destPath}", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Lỗi", "Có lỗi xảy ra khi ghi file WAV mới.", "OK");
            }
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("Lỗi khi cắt âm thanh: " + ex);
            EditorUtility.DisplayDialog("Lỗi", "Lỗi: " + ex.Message, "OK");
        }
        finally
        {
            // Restore streaming configuration if we changed it
            if (wasStreaming && importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = settings;
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
    }

    private bool SaveWavFile(string filePath, float[] samples, int channels, int frequency)
    {
        try
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
            string folderPath = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            using (FileStream fileStream = new FileStream(absolutePath, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    // RIFF header
                    writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                    writer.Write(36 + samples.Length * 2); // File size minus 8 bytes
                    writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
                    
                    // fmt chunk
                    writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                    writer.Write(16); // Chunk size (16 for PCM)
                    writer.Write((ushort)1); // Audio format (1 = PCM)
                    writer.Write((ushort)channels);
                    writer.Write(frequency);
                    writer.Write(frequency * channels * 2); // Byte rate
                    writer.Write((ushort)(channels * 2)); // Block align (channels * bytes_per_sample)
                    writer.Write((ushort)16); // Bits per sample (16-bit PCM)
                    
                    // data chunk
                    writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                    writer.Write(samples.Length * 2); // Chunk size (bytes)
                    
                    // Write samples as 16-bit signed PCM integers
                    for (int i = 0; i < samples.Length; i++)
                    {
                        short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                        writer.Write(value);
                    }
                }
            }
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error writing WAV file: " + e.Message);
            return false;
        }
    }

    private string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        float s = seconds % 60f;
        return $"{m}:{s:00.000}";
    }

    private bool TryParseTime(string input, out float seconds)
    {
        seconds = 0f;
        if (float.TryParse(input, out float sec))
        {
            seconds = sec;
            return true;
        }
        
        string[] parts = input.Split(':');
        if (parts.Length == 2)
        {
            if (float.TryParse(parts[0], out float m) && float.TryParse(parts[1], out float s))
            {
                seconds = m * 60f + s;
                return true;
            }
        }
        return false;
    }
}
