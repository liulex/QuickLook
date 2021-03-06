﻿// Copyright © 2017 Paddy Xu
// 
// This file is part of QuickLook program.
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Meta.Vlc;
using Meta.Vlc.Interop.Media;
using QuickLook.Annotations;
using MediaState = Meta.Vlc.Interop.Media.MediaState;

namespace QuickLook.Plugin.VideoViewer
{
    /// <summary>
    ///     Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class ViewerPanel : UserControl, IDisposable, INotifyPropertyChanged
    {
        private readonly ContextObject _context;

        private string _coverArt;
        private bool _hasAudio;
        private bool _hasEnded;
        private bool _hasVideo;
        private bool _isMuted;
        private bool _isPlaying;
        private bool _wasPlaying;

        public ViewerPanel(ContextObject context)
        {
            InitializeComponent();

            mediaElement.PropertyChanged += PlayerPropertyChanged;
            mediaElement.StateChanged += PlayerStateChanged;

            _context = context;

            buttonPlayPause.MouseLeftButtonUp += TogglePlayPause;
            //buttonMute.MouseLeftButtonUp += (sender, e) =>
            //{
            //    mediaElement.IsMuted = false;
            //    buttonMute.Visibility = Visibility.Collapsed;
            //};
            buttonMute.MouseLeftButtonUp += (sender, e) => IsMuted = !IsMuted;
            buttonStop.MouseLeftButtonUp += PlayerStop;
            buttonBackward.MouseLeftButtonUp += (sender, e) => Seek(TimeSpan.FromSeconds(-10));
            buttonForward.MouseLeftButtonUp += (sender, e) => Seek(TimeSpan.FromSeconds(10));

            sliderProgress.PreviewMouseDown += (sender, e) =>
            {
                _wasPlaying = mediaElement.VlcMediaPlayer.IsPlaying;
                mediaElement.Pause();
            };
            sliderProgress.PreviewMouseUp += (sender, e) =>
            {
                if (_wasPlaying) mediaElement.Play();
            };

            mediaElement.VlcMediaPlayer.EncounteredError += ShowErrorNotification;
            /*mediaElement.MediaEnded += (s, e) =>
            {
                if (mediaElement.IsOpen)
                    if (!mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        mediaElement.Stop();
                        mediaElement.Play();
                    }
            };*/

            PreviewMouseWheel += (sender, e) => ChangeVolume((double) e.Delta / 120 * 2);
        }

        public bool IsMuted
        {
            get => _isMuted;
            set
            {
                if (value == _isMuted) return;
                _isMuted = value;
                mediaElement.IsMute = value;
                OnPropertyChanged();
            }
        }

        public bool HasEnded
        {
            get => _hasEnded;
            private set
            {
                if (value == _hasEnded) return;
                _hasEnded = value;
                OnPropertyChanged();
            }
        }

        public bool HasAudio
        {
            get => _hasAudio;
            private set
            {
                if (value == _hasAudio) return;
                _hasAudio = value;
                OnPropertyChanged();
            }
        }

        public bool HasVideo
        {
            get => _hasVideo;
            private set
            {
                if (value == _hasVideo) return;
                _hasVideo = value;
                OnPropertyChanged();
            }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            private set
            {
                if (value == _isPlaying) return;
                _isPlaying = value;
                OnPropertyChanged();
            }
        }

        public string CoverArt
        {
            get => _coverArt;
            private set
            {
                if (value == _coverArt) return;
                if (value == null) return;
                _coverArt = value;
                OnPropertyChanged();
            }
        }

        public string LibVlcPath { get; } = VlcSettings.LibVlcPath;

        public string[] VlcOption { get; } = VlcSettings.VlcOptions;

        public void Dispose()
        {
            try
            {
                Task.Run(() =>
                {
                    mediaElement?.Dispose();
                    mediaElement = null;
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void PlayerStop(object sender, MouseButtonEventArgs e)
        {
            HasEnded = false;
            IsPlaying = false;
            mediaElement.Position = 0;
            mediaElement.Stop();
        }

        private void PlayerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var prop = e.PropertyName;

            switch (prop)
            {
                case nameof(mediaElement.IsMute):
                    IsMuted = mediaElement.IsMute;
                    break;
            }
        }

        private void PlayerStateChanged(object sender, ObjectEventArgs<MediaState> e)
        {
            var state = e.Value;

            switch (state)
            {
                case MediaState.Opening:
                    HasVideo = mediaElement.VlcMediaPlayer.VideoTrackCount > 0;
                    HasAudio = mediaElement.VlcMediaPlayer.AudioTrackCount > 0;
                    break;
                case MediaState.Playing:
                    CoverArt = mediaElement.VlcMediaPlayer.Media.GetMeta(
                        MetaDataType.ArtworkUrl);
                    HasVideo = mediaElement.VlcMediaPlayer.VideoTrackCount > 0;
                    HasAudio = mediaElement.VlcMediaPlayer.AudioTrackCount > 0;
                    IsPlaying = true;
                    break;
                case MediaState.Paused:
                    IsPlaying = false;
                    break;
                case MediaState.Ended:
                    IsPlaying = false;
                    HasEnded = true;
                    break;
                case MediaState.Error:
                    ShowErrorNotification(sender, e);
                    break;
            }
        }

        private void ChangeVolume(double delta)
        {
            IsMuted = false;

            var newVol = mediaElement.Volume + (int) delta;
            newVol = Math.Max(newVol, 0);
            newVol = Math.Min(newVol, 100);

            mediaElement.Volume = newVol;
        }

        private void Seek(TimeSpan delta)
        {
            _wasPlaying = mediaElement.VlcMediaPlayer.IsPlaying;
            mediaElement.Pause();

            mediaElement.Time += delta;

            if (_wasPlaying) mediaElement.Play();
        }

        private void TogglePlayPause(object sender, MouseButtonEventArgs e)
        {
            if (mediaElement.VlcMediaPlayer.IsPlaying)
            {
                mediaElement.Pause();
            }
            else
            {
                if (HasEnded)
                    mediaElement.Stop();
                mediaElement.Play();
            }
        }

        [DebuggerNonUserCode]
        private void ShowErrorNotification(object sender, EventArgs e)
        {
            _context.ShowNotification("", "An error occurred while loading the video.");
            mediaElement?.Stop();

            Dispose();


            //throw new Exception("fallback to default viewer.");
        }

        public void LoadAndPlay(string path)
        {
            mediaElement.LoadMedia(path);
            mediaElement.Volume = 50;

            mediaElement.Play();
        }

        ~ViewerPanel()
        {
            GC.SuppressFinalize(this);
            Dispose();
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}