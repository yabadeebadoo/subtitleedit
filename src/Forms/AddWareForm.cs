﻿using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class AddWareForm : Form
    {
        public string SourceVideoFileName { get; private set; }
        private bool _cancel = false;
        private string _wavFileName = null;
        private string _spectrogramDirectory;
        public List<Bitmap> SpectrogramBitmaps { get; private set; }
        private string _encodeParamters;
        private const string RetryEncodeParameters = "acodec=s16l";
        private int _audioTrackNumber = -1;
        private int _delayInMilliseconds = 0;

        public AddWareForm()
        {
            InitializeComponent();
            labelProgress.Text = string.Empty;
            buttonCancel.Visible = false;
            labelInfo.Text = string.Empty;
        }

        public WavePeakGenerator WavePeak { get; private set; }

        public void Initialize(string videoFile, string spectrogramDirectory, int audioTrackNumber)
        {
            _audioTrackNumber = audioTrackNumber;
            if (_audioTrackNumber < 0)
                _audioTrackNumber = 0;
            Text = Configuration.Settings.Language.AddWaveForm.Title;
            buttonRipWave.Text = Configuration.Settings.Language.AddWaveForm.GenerateWaveFormData;
            labelPleaseWait.Text = Configuration.Settings.Language.AddWaveForm.PleaseWait;
            labelVideoFileName.Text = videoFile;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            labelSourcevideoFile.Text = Configuration.Settings.Language.AddWaveForm.SourceVideoFile;
            _spectrogramDirectory = spectrogramDirectory;
            _encodeParamters = Configuration.Settings.General.VlcWaveTranscodeSettings;
        }

        private void buttonRipWave_Click(object sender, EventArgs e)
        {
            buttonRipWave.Enabled = false;
            _cancel = false;
            bool runningOnWindows = false;
            SourceVideoFileName = labelVideoFileName.Text;
            string targetFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".wav");
            string parameters = "\"" + SourceVideoFileName + "\" -I dummy -vvv --no-sout-video --audio-track=" + _audioTrackNumber.ToString() + " --sout=\"#transcode{acodec=s16l,channels=1,ab=128}:std{access=file,mux=wav,dst=" + targetFile + "}\" vlc://quit";
//            string parameters = "\"" + SourceVideoFileName + "\" -I dummy -vvv --no-sout-video --audio-track=" + _audioTrackNumber.ToString() + " --sout=\"#transcode{acodec=s16l,channels=2,ab=128,samplerate=24000}:std{access=file,mux=wav,dst=" + targetFile + "}\" vlc://quit";
            string exeFilePath;
            if (Utilities.IsRunningOnLinux() || Utilities.IsRunningOnMac())
            {
                exeFilePath = "cvlc";
                parameters = "-vvv --no-sout-video --audio-track=" + _audioTrackNumber.ToString() + " --sout '#transcode{" + _encodeParamters + "}:std{mux=wav,access=file,dst=" + targetFile + "}' \"" + SourceVideoFileName + "\" vlc://quit";
            }
            else // windows
            {
                runningOnWindows = true;
                exeFilePath = Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic.GetVlcPath("vlc.exe");
                if (!System.IO.File.Exists(exeFilePath))
                {
                    if (Configuration.Settings.General.UseFFMPEGForWaveExtraction && File.Exists(Configuration.Settings.General.FFMPEGLocation))
                    {
                        // We will run FFMPEG
                    }
                    else
                    {

                        if (MessageBox.Show(Configuration.Settings.Language.AddWaveForm.VlcMediaPlayerNotFound + Environment.NewLine +
                                            Environment.NewLine +
                                            Configuration.Settings.Language.AddWaveForm.GoToVlcMediaPlayerHomePage,
                                           Configuration.Settings.Language.AddWaveForm.VlcMediaPlayerNotFoundTitle, MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start("http://www.videolan.org/");
                        }
                        buttonRipWave.Enabled = true;
                        return;
                    }
                }
            }

            labelInfo.Text = "VLC";
            if (Configuration.Settings.General.UseFFMPEGForWaveExtraction && File.Exists(Configuration.Settings.General.FFMPEGLocation))
            {
                string FFMPEGWaveTranscodeSettings = "-i \"{0}\" -vn -ar 24000 -ac 2 -ab 128 -vol 448 -f wav \"{1}\"";
                //-i indicates the input
                //-vn means no video ouput
                //-ar 44100 indicates the sampling frequency.
                //-ab indicates the bit rate (in this example 160kb/sec)
                //-vol 448 will boot volume... 256 is normal
                //-ac 2 means 2 channels

                exeFilePath = Configuration.Settings.General.FFMPEGLocation;
                parameters = string.Format(FFMPEGWaveTranscodeSettings, SourceVideoFileName, targetFile);
                labelInfo.Text = "FFMPEG";
            }

            labelPleaseWait.Visible = true;
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo(exeFilePath, parameters);
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.Visible = true;
            double seconds = 0;
            buttonCancel.Visible = true;
            try
            {
                process.PriorityClass = ProcessPriorityClass.Normal;
            }
            catch
            {
            }
            while (!process.HasExited)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(100);
                seconds += 0.1;
                if (seconds < 60)
                    labelProgress.Text = string.Format(Configuration.Settings.Language.AddWaveForm.ExtractingSeconds, seconds);
                else
                    labelProgress.Text = string.Format(Configuration.Settings.Language.AddWaveForm.ExtractingMinutes, (int)(seconds / 60), (int)(seconds % 60));
                this.Refresh();
                if (_cancel)
                {
                    process.Kill();
                    progressBar1.Visible = false;
                    labelPleaseWait.Visible = false;
                    buttonRipWave.Enabled = true;
                    targetFile = null;
                    buttonCancel.Visible = false;
                    DialogResult = DialogResult.Cancel;
                    return;
                }

                if (seconds > 1 && Convert.ToInt32(seconds) % 10 == 0 && runningOnWindows)
                {
                    try
                    {
                        var drive = new DriveInfo("c");
                        if (drive.IsReady)
                        {
                            if (drive.AvailableFreeSpace < 50 * 1000000) // 50 mb
                            {
                                labelInfo.ForeColor = Color.Red;
                                labelInfo.Text = "LOW DISC SPACE!";
                            }
                            else if (labelInfo.ForeColor == Color.Red)
                            {
                                labelInfo.Text = Utilities.FormatBytesToDisplayFileSize(drive.AvailableFreeSpace) + " free";
                            }
                        }
                    }
                    catch
                    {
                    }
                }

            }
            buttonCancel.Visible = false;
            progressBar1.Visible = false;
            progressBar1.Style = ProgressBarStyle.Blocks;

            if (!File.Exists(targetFile))
            {
                if (_encodeParamters != RetryEncodeParameters)
                {
                    _encodeParamters = RetryEncodeParameters;
                    buttonRipWave_Click(null, null);
                    return;
                }

                MessageBox.Show("Could not find extracted wave file! This feature requires VLC media player 1.1.x or newer (32-bit)." + Environment.NewLine
                                + Environment.NewLine +
                                "Command line: " + exeFilePath + " " + parameters);

                labelPleaseWait.Visible = false;
                labelProgress.Text = string.Empty;
                buttonRipWave.Enabled = true;
                return;
            }

            FileInfo fi = new FileInfo(targetFile);
            if (fi.Length <= 200)
            {
                MessageBox.Show("Sorry! VLC/FFMPEG was unable to extract audio to wave file via this command line:" + Environment.NewLine +
                                Environment.NewLine +
                                "Command line: " + exeFilePath + " " + parameters + Environment.NewLine +
                                Environment.NewLine +
                                "Note: Do check free disk space.");
                labelPleaseWait.Visible = false;
                labelProgress.Text = string.Empty;
                buttonRipWave.Enabled = true;
                return;
            }

            ReadWaveFile(targetFile, _delayInMilliseconds);
            labelProgress.Text = string.Empty;
            File.Delete(targetFile);
            this.DialogResult = DialogResult.OK;
        }

        private void ReadWaveFile(string targetFile, int delayInMilliseconds)
        {
            var waveFile = new WavePeakGenerator(targetFile);

            int sampleRate = Configuration.Settings.VideoControls.WaveFormMininumSampleRate; // Normally 128
            while (!(waveFile.Header.SampleRate % sampleRate == 0) && sampleRate < 5000)
                sampleRate++; // old sample-rate / new sample-rate must have rest = 0

            labelProgress.Text = Configuration.Settings.Language.AddWaveForm.GeneratingPeakFile;
            this.Refresh();
            waveFile.GeneratePeakSamples(sampleRate, delayInMilliseconds); // samples per second - SampleRate

            if (Configuration.Settings.VideoControls.GenerateSpectrogram)
            {
                labelProgress.Text = Configuration.Settings.Language.AddWaveForm.GeneratingSpectrogram;
                this.Refresh();
                System.IO.Directory.CreateDirectory(_spectrogramDirectory);
                SpectrogramBitmaps = waveFile.GenerateFourierData(256, _spectrogramDirectory, delayInMilliseconds); // image height = nfft / 2
            }
            labelPleaseWait.Visible = false;

            WavePeak = waveFile;
            waveFile.Close();
        }

        private void AddWareForm_Shown(object sender, EventArgs e)
        {
            Refresh();
            var audioTrackNames = new List<string>();
            var mkvAudioTrackNumbers = new Dictionary<int, int>();
            int numberOfAudioTracks = 0;
            if (labelVideoFileName.Text.Length > 1 && File.Exists(labelVideoFileName.Text))
            {
                if (labelVideoFileName.Text.ToLower().EndsWith(".mkv"))
                { // Choose for number of audio tracks in matroska files
                    try
                    {
                        var mkv = new Matroska(labelVideoFileName.Text);
                        if (mkv.IsValid)
                        {
                            var trackInfo = mkv.GetTrackInfo();
                            foreach (var ti in trackInfo)
                            {
                                if (ti.IsAudio)
                                {
                                    numberOfAudioTracks++;
                                    if (ti.CodecId != null && ti.Language != null)
                                        audioTrackNames.Add("#" + ti.TrackNumber + ": " + ti.CodecId.Replace("\0", string.Empty) + " - " + ti.Language.Replace("\0", string.Empty));
                                    else
                                        audioTrackNames.Add("#" + ti.TrackNumber.ToString());
                                    mkvAudioTrackNumbers.Add(mkvAudioTrackNumbers.Count, ti.TrackNumber);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
                else if (labelVideoFileName.Text.ToLower().EndsWith(".mp4") || labelVideoFileName.Text.ToLower().EndsWith(".m4v"))
                { // Choose for number of audio tracks in mp4 files
                    try
                    {
                        var mp4 = new Nikse.SubtitleEdit.Logic.Mp4.Mp4Parser(labelVideoFileName.Text);
                        var tracks = mp4.GetAudioTracks();
                        int i=0;
                        foreach (var track in tracks)
                        {
                            i++;
                            if (track.Name != null && track.Mdia != null && track.Mdia.Mdhd != null && track.Mdia.Mdhd.LanguageString != null)
                                audioTrackNames.Add(i + ":  " + track.Name + " - " + track.Mdia.Mdhd.LanguageString);
                            else if (track.Name != null)
                                audioTrackNames.Add(i + ":  " + track.Name);
                            else
                                audioTrackNames.Add(i.ToString());
                        }
                        numberOfAudioTracks = tracks.Count;
                    }
                    catch
                    {
                    }
                }

                if (Configuration.Settings.General.UseFFMPEGForWaveExtraction)
                { // don't know how to extract audio number x via FFMPEG...
                    numberOfAudioTracks = 1;
                    _audioTrackNumber = 0;
                }

                // Choose audio track
                if (numberOfAudioTracks > 1)
                {
                    var form = new ChooseAudioTrack(audioTrackNames, _audioTrackNumber);
                    if (form.ShowDialog(this) == DialogResult.OK)
                    {
                        _audioTrackNumber = form.SelectedTrack;
                    }
                    else
                    {
                        DialogResult = DialogResult.Cancel;
                        return;
                    }
                }

                // check for delay in matroska files
                if (labelVideoFileName.Text.ToLower().EndsWith(".mkv"))
                {
                    try
                    {
                        var mkv = new Matroska(labelVideoFileName.Text);
                        if (mkv.IsValid)
                        {
                            _delayInMilliseconds = (int)mkv.GetTrackStartTime(mkvAudioTrackNumbers[_audioTrackNumber]);
                        }
                    }
                    catch
                    {
                        _delayInMilliseconds = 0;
                    }
                }

                buttonRipWave_Click(null, null);
            }
            else if (_wavFileName != null)
            {
                FixWaveOnly();
            }
        }

        private void AddWareForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
            else if (e.KeyCode == Keys.F1)
            {
                Utilities.ShowHelp("#waveform");
                e.SuppressKeyPress = true;
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            _cancel = true;
        }

        internal void InitializeViaWaveFile(string fileName, string spectrogramFolder)
        {
            _wavFileName = fileName;
            _spectrogramDirectory = spectrogramFolder;
        }

        private void FixWaveOnly()
        {
            Text = Configuration.Settings.Language.AddWaveForm.Title;
            buttonRipWave.Text = Configuration.Settings.Language.AddWaveForm.GenerateWaveFormData;
            labelPleaseWait.Text = Configuration.Settings.Language.AddWaveForm.PleaseWait;
            labelVideoFileName.Text = string.Empty;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            buttonRipWave.Enabled = false;
            _cancel = false;
            buttonCancel.Visible = false;
            progressBar1.Visible = false;
            progressBar1.Style = ProgressBarStyle.Blocks;

            labelProgress.Text = Configuration.Settings.Language.AddWaveForm.GeneratingPeakFile;
            this.Refresh();
            labelPleaseWait.Visible = false;
            try
            {
                ReadWaveFile(_wavFileName, _delayInMilliseconds);
                labelProgress.Text = string.Empty;
                this.DialogResult = DialogResult.OK;
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + Environment.NewLine + exception.StackTrace);
                this.DialogResult = DialogResult.Cancel;
            }
        }
    }
}
