using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace caelestia.Services
{
    public class FftSampleProvider : ISampleProvider
    {
        private const int FftLength = 1024;
        private readonly float[] peakLevels = new float[48];
        private const int M = 10;
        private readonly ISampleProvider source;
        private readonly Complex[] fftBuffer;
        public event Action<float[]>? SpectrumCalculated;
        private int fftPos;

        private struct Band
        {
            public int Start;
            public int End;
        }
        private readonly Band[] bands = new Band[48];

        public WaveFormat WaveFormat => source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = buffer[offset + i];

                fftBuffer[fftPos].X = (float)(sample * FastFourierTransform.HammingWindow(fftPos, FftLength));

                fftBuffer[fftPos].Y = 0;

                fftPos++;

                if (fftPos >= FftLength)
                {
                    fftPos = 0;

                    ProcessFFT();
                }
            }

            return samplesRead;
        }
        private void BuildBands()
        {
            int fftBins = FftLength / 2;

            double minFreq = 400;
            double maxFreq = 10000;
            double gamma = 3;

            double sampleRate = source.WaveFormat.SampleRate;

            for (int i = 0; i < bands.Length; i++)
            {
                double t1 = Math.Pow((double)i / bands.Length, gamma);
                double t2 = Math.Pow((double)(i + 1) / bands.Length, gamma);

                double f1 = minFreq + t1 * (maxFreq - minFreq);
                double f2 = minFreq + t2 * (maxFreq - minFreq);

                int start = (int)(f1 / (sampleRate / FftLength));
                int end = (int)(f2 / (sampleRate / FftLength));

                start = Math.Clamp(start, 1, fftBins - 1);
                end = Math.Clamp(end, start + 1, fftBins);

                bands[i].Start = start;
                bands[i].End = end;
            }
        }

        private void ProcessFFT()
        {

            Complex[] fft = new Complex[FftLength];

            Array.Copy(fftBuffer, fft, FftLength);

            FastFourierTransform.FFT(true, M, fft);

            float[] spectrum = new float[48];


            for (int i = 0; i < spectrum.Length; i++)
            {
                float sum = 0;
                int count = 0;

                for (int index = bands[i].Start; index < bands[i].End;index++)
                {

                    float magnitude = (float)Math.Sqrt(
                        fft[index].X * fft[index].X +
                        fft[index].Y * fft[index].Y);
                    magnitude /= (FftLength / 2f);

                    sum += magnitude;   // <-- HERE
                    count++;




                }
                float average = sum / count;
                if (average > peakLevels[i])
                    peakLevels[i] = average;
                else
                    peakLevels[i] *= 0.995f;

                    float normalized = average / Math.Max(peakLevels[i],0.0001f);

                spectrum[i] = MathF.Pow(normalized, 0.5f);

            }

            SpectrumCalculated?.Invoke(spectrum);
        }
        //constructor
        public FftSampleProvider(ISampleProvider source)
        {
            this.source = source;

            fftBuffer = new Complex[FftLength];

            BuildBands();
        }
    }


}
