using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ScriptPlayer.Shared
{
    public class VideoInfoWrapper : FfmpegWrapper
    {
        public TimeSpan Duration { get; private set; }

        public string AudioCodec { get; private set; }

        public double SampleRate { get; private set; }

        public string VideoCodec { get; private set; }

        public Resolution Resolution { get; private set; }

        public double FrameRate { get; private set; }

        public void DumpInfo()
        {
            Debug.WriteLine("Duration:   " + Duration);
            Debug.WriteLine("AudioCodec: " + AudioCodec);
            Debug.WriteLine("SampleRate: " + SampleRate);
            Debug.WriteLine("VideoCodec: " + VideoCodec);
            Debug.WriteLine("Resolution: " + Resolution);
            Debug.WriteLine("FrameRate:  " + FrameRate);
        }

        public bool IsComplete()
        {
            return Duration > TimeSpan.Zero &&
                   !string.IsNullOrWhiteSpace(AudioCodec) &&
                   !string.IsNullOrWhiteSpace(VideoCodec) &&
                   SampleRate > 0 &&
                   Resolution.Horizontal > 0 &&
                   Resolution.Vertical > 0 &&
                   FrameRate > 0;
        }

        public bool IsGoodEnough()
        {
            return Duration > TimeSpan.Zero && 
                   !string.IsNullOrWhiteSpace(VideoCodec) &&
                   Resolution.Horizontal > 0 &&
                   Resolution.Vertical > 0;
        }

        public VideoInfoWrapper(string ffmpegExe) : base(ffmpegExe)
        {
            Duration = TimeSpan.Zero;
        }

        protected override void SetArguments()
        {
            Arguments = $"-i \"{VideoFile}\" -hide_banner";
        }

        // Duration: 00:02:17.59, start: 0.000000, bitrate: 11106 kb/s
        private readonly Regex _durationRegex = new Regex(@"^\s*Duration:\s*(?<Duration>\d{2}:\d{2}:\d{2}\.\d{2})", RegexOptions.Compiled);

        // Stream #0:0(eng): Audio: aac (LC) (mp4a / 0x6134706D), 48000 Hz, stereo, fltp, 127 kb/s (default)
        private readonly Regex _audioRegex = new Regex(@"^(?<Codec>[^,]*?)(\(.*?\))?, (?<SampleRate>[^,]*?) Hz,", RegexOptions.Compiled);

        // Stream #0:1(eng): Video: h264 (High) (avc1 / 0x31637661), yuv420p, 1920x1080 [SAR 1:1 DAR 16:9], 10971 kb/s, 59.94 fps, 59.94 tbr, 60k tbn, 119.88 tbc (default)
        private readonly Regex _videoRegex = new Regex(@"^(?<Codec>[^,]*?)(\(.*?\))*,([^,]*?,\s*)*(?<Resolution>\d+x\d+)([^,]*?, \s*)*((?<FrameRate>[^,]*?) fps)?", RegexOptions.Compiled);

        private readonly Regex _detailRegex = new Regex(@"((\s*)(?<content>[^,\(\)]*)(\s*\([^\(\)]*\)\s*)*,?)*");

        private readonly Regex _resolutionRegex = new Regex(@"(?<Width>\d+)x(?<Height>\d+)");

        private readonly Regex _sampleRateRegex = new Regex(@"(?<SampleRate>.*?) Hz");

        private readonly Regex _frameRateRegex = new Regex(@"(?<FrameRate>.*?) fps");

        private readonly Regex _streamRegex = new Regex(@"^\s*Stream #\d:\d(.*?):\s*(?<Type>.*?):\s*(?<Details>.*)\s*$", RegexOptions.Compiled);

        protected override void ProcessLine(string line, bool isError)
        {
            base.ProcessLine(line, isError);

            Match durationMatch = _durationRegex.Match(line);
            if (durationMatch.Success)
            {
                string duraString = _durationRegex.Match(line).Groups["Duration"].Value;
                Debug.WriteLine("DURATION: " + duraString);

                Duration = TimeSpan.ParseExact(duraString, "hh\\:mm\\:ss\\.ff", CultureInfo.InvariantCulture);
                return;
            }

            Match streamRegex = _streamRegex.Match(line);
            if (streamRegex.Success)
            {
                string type = streamRegex.Groups["Type"].Value;
                string details = streamRegex.Groups["Details"].Value;

                Match detailMatches = _detailRegex.Match(details);
                if (!detailMatches.Success)
                    return;

                switch (type)
                {
                    case "Data":
                    {
                        break;
                    }
                    case "Audio":
                    {
                        if (!string.IsNullOrEmpty(AudioCodec))
                            return;

                        AudioCodec = detailMatches.Groups["content"].Captures[0].Value;

                        foreach (Capture capture in detailMatches.Groups["content"].Captures)
                        {
                            Match sampleMatch = _sampleRateRegex.Match(capture.Value);
                            if(sampleMatch.Success)
                            {
                                if (double.TryParse(sampleMatch.Groups["SampleRate"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double sampleRate))
                                    SampleRate = sampleRate;
                            }
                        }

                        break;
                    }
                    case "Video":
                    {
                        if (!string.IsNullOrEmpty(VideoCodec))
                            return;

                        VideoCodec = detailMatches.Groups["content"].Captures[0].Value;

                        foreach (Capture capture in detailMatches.Groups["content"].Captures)
                        {
                            Match resolutionMatch = _resolutionRegex.Match(capture.Value);
                            if (resolutionMatch.Success)
                            {
                                if (Resolution.TryParse(resolutionMatch.Value, out Resolution resolution))
                                {
                                    Resolution = resolution;
                                    continue;
                                }
                            }

                            Match framerateMatch = _frameRateRegex.Match(capture.Value);
                            if (framerateMatch.Success)
                            {
                                if (double.TryParse(framerateMatch.Groups["FrameRate"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double frameRate))
                                    FrameRate = frameRate;
                            }
                        }

                        break;
                    }
                    case "Subtitle":
                    {
                        break;
                    }
                    case "Attachment":
                    {
                        break;
                    }
                    default:
                    {
                        break;
                    }
                }
            }
        }
    }
}