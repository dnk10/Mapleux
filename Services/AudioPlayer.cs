using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace caelestia.Services
{

    class AudioPlayer
    {
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;
        public event Action<float[]>? SpectrumCalculated;
        // private MeteringSampleProvider? meter;
        public event Action<float>? VolumeChanged;
        private FftSampleProvider fftProvider;

        public void Play()
        {
            outputDevice?.Play();
        }
        private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (audioFile != null && outputDevice != null)
            {
                audioFile.Position = 0;   // Rewind to the beginning
                outputDevice.Play();      // Play again
            }
        }

        public void Pause()
        {
            outputDevice?.Pause();
        }

        public void Dispose()
        {
            outputDevice?.Dispose();
            audioFile?.Dispose();
        }

        public float Volume
        {
            get => audioFile?.Volume ?? 1.0f;
            set
            {
                if (audioFile != null)
                    audioFile.Volume = Math.Clamp(value, 0f, 1f);
            }
        }

        private void Meter_StreamVolume(object? sender, StreamVolumeEventArgs e)
        {
            float max = 0;

            foreach (float sample in e.MaxSampleValues)
            {
                if (sample > max)
                    max = sample;
            }

            VolumeChanged?.Invoke(max);
        }

        //Constructor
        public AudioPlayer(string path)
        {
            audioFile = new AudioFileReader(path);

            outputDevice = new WaveOutEvent();

            fftProvider = new FftSampleProvider(audioFile);

            outputDevice.Init(fftProvider);

            fftProvider.SpectrumCalculated += spectrum =>
            {
                SpectrumCalculated?.Invoke(spectrum);
            };

            outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;


        }
    }
}
