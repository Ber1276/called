using NAudio.Wave;
using NAudio.CoreAudioApi;
using System.IO;

namespace CalledAssistant.Services
{
    public class AudioCaptureService : IDisposable
    {
        private WasapiCapture? _capture;
        private MemoryStream? _audioStream;
        private WaveFileWriter? _waveWriter;
        private bool _isRecording = false;
        private bool _hadSound = false;

        public event Action<byte[], WaveFormat>? RecordingCompleted;
        public event Action<float>? VolumeChanged; // 0.0 ~ 1.0

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            if (_isRecording) return;

            _audioStream = new MemoryStream();
            _capture = new WasapiCapture();
            _capture.WaveFormat = new WaveFormat(16000, 16, 1);
            _waveWriter = new WaveFileWriter(_audioStream, _capture.WaveFormat);

            _hadSound = false;

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            _isRecording = true;
        }

        public void StopRecording()
        {
            if (!_isRecording) return;
            _capture?.StopRecording();
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_waveWriter == null || e.BytesRecorded == 0) return;

            _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);

            // 计算 RMS 音量用于可视化
            double rms = CalculateRms(e.Buffer, e.BytesRecorded);
            float volume = (float)Math.Min(1.0, rms * 10.0);
            VolumeChanged?.Invoke(volume);

            if (rms > 0.01)
                _hadSound = true;
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isRecording = false;
            _waveWriter?.Flush();

            if (_audioStream != null && _hadSound)
            {
                var format = _capture!.WaveFormat;
                _audioStream.Position = 0;
                var audioData = _audioStream.ToArray();
                RecordingCompleted?.Invoke(audioData, format);
            }

            Cleanup();
        }

        private static double CalculateRms(byte[] buffer, int bytesRecorded)
        {
            double sum = 0;
            int samples = bytesRecorded / 2; // 16-bit samples
            for (int i = 0; i < bytesRecorded - 1; i += 2)
            {
                short sample = (short)(buffer[i] | buffer[i + 1] << 8);
                double normalized = sample / 32768.0;
                sum += normalized * normalized;
            }
            return samples > 0 ? Math.Sqrt(sum / samples) : 0;
        }

        private void Cleanup()
        {
            if (_capture != null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.RecordingStopped -= OnRecordingStopped;
                _capture.Dispose();
                _capture = null;
            }

            _waveWriter?.Dispose();
            _waveWriter = null;
            _audioStream?.Dispose();
            _audioStream = null;
        }

        public void Dispose()
        {
            if (_isRecording) _capture?.StopRecording();
            else Cleanup();
        }
    }
}
