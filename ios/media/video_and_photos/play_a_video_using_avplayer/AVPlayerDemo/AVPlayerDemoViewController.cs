using System;

using Foundation;
using UIKit;
using AVFoundation;
using CoreGraphics;
using System.Timers;
using CoreMedia;

namespace AVPlayerDemo
{
    public partial class AVPlayerDemoViewController : UIViewController
    {
        AVPlayer _player;
        AVPlayerLayer _playerLayer;
        AVAsset _asset;
        AVPlayerItem _playerItem;
		Timer touchTimer;
		float RestoreAfterScrubbingRate;
		bool IsSeeking;
		NSObject TimeObserver;
		const int NSEC_PER_SEC = 1000000000;

		NSObject _aVPlayerItemFailedToPlayToEndTimeNotificationObserver;
		NSObject _aVPlayerItemDidPlayToEndTimeNotificationObserver;
		NSObject _aVPlayerItemPlaybackStalledNotificationObserver;
		NSObject _aVPlayerItemNewErrorLogEntryNotificationObserver;
		NSObject _aVPlayerItemNewAccessLogEntryNotificationObserver;

		public static NSString StatusObservationContext = new NSString("AVCustomEditPlayerViewControllerStatusObservationContext");
		public static NSString PlaybackBufferEmptyContext = new NSString("PlaybackBufferEmptyContext");
		public static NSString PlaybackBufferFullContext = new NSString("PlaybackBufferFullContext");
		public static NSString PlaybackLikelyToKeepUpContext = new NSString("PlaybackLikelyToKeepUpContext");

        public AVPlayerDemoViewController () : base ("AVPlayerDemoViewController", null)
        {
        }
        
        public override void DidReceiveMemoryWarning ()
        {
            // Releases the view if it doesn't have a superview.
            base.DidReceiveMemoryWarning ();
            
            // Release any cached data, images, etc that aren't in use.
        }
        
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            
			LoadNotifications ();
			IsSeeking = false;
			_asset = AVAsset.FromUrl (NSUrl.FromFilename ("sample.m4v"));
			_playerItem = new AVPlayerItem (_asset);

			_player = new AVPlayer (_playerItem); 
			_playerItem.AddObserver (this, (NSString)"status", NSKeyValueObservingOptions.New|NSKeyValueObservingOptions.Initial, StatusObservationContext.Handle);
			_playerItem.AddObserver (this, (NSString)"playbackBufferFull", NSKeyValueObservingOptions.New|NSKeyValueObservingOptions.Initial, PlaybackBufferFullContext.Handle);
			_playerItem.AddObserver (this, (NSString)"playbackBufferEmpty", NSKeyValueObservingOptions.New|NSKeyValueObservingOptions.Initial, PlaybackBufferEmptyContext.Handle);
			_playerItem.AddObserver (this, (NSString)"playbackLikelyToKeepUp", NSKeyValueObservingOptions.New|NSKeyValueObservingOptions.Initial, PlaybackLikelyToKeepUpContext.Handle);

			_player.Muted = true;
            _playerLayer = AVPlayerLayer.FromPlayer (_player);
            _playerLayer.Frame = View.Frame;
            
            View.Layer.AddSublayer (_playerLayer);

			AddTapGesture ();
			InitScrubberTimer ();
			SyncScrubber ();
            
            _player.Play ();
			PlayButton.SetTitle ("Pause", UIControlState.Normal);
        }

		void ReleasePlayerItem()
		{
			if (_playerItem != null) {
				_playerItem.CancelPendingSeeks ();
				_playerItem.RemoveObserver (this, (NSString)"status");
				_playerItem.RemoveObserver (this, (NSString)"playbackBufferFull");
				_playerItem.RemoveObserver (this, (NSString)"playbackBufferEmpty");
				_playerItem.RemoveObserver (this, (NSString)"playbackLikelyToKeepUp");

				_playerItem = null;
			}
		}

		void LoadNotifications()
		{
			_aVPlayerItemFailedToPlayToEndTimeNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver ((NSString)"AVPlayerItemFailedToPlayToEndTimeNotification", PlayerPlaybackError);
			_aVPlayerItemDidPlayToEndTimeNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver ((NSString)"AVPlayerItemDidPlayToEndTimeNotification", PlayerPlaybackDidFinish);
			_aVPlayerItemPlaybackStalledNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver ((NSString)"AVPlayerItemPlaybackStalledNotification", AVPlayerItemPlaybackStalled);
			_aVPlayerItemNewErrorLogEntryNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver ((NSString)"AVPlayerItemNewErrorLogEntryNotification", AVPlayerItemNewErrorLogEntry);
			_aVPlayerItemNewAccessLogEntryNotificationObserver = NSNotificationCenter.DefaultCenter.AddObserver ((NSString)"AVPlayerItemNewAccessLogEntryNotification", AVPlayerItemNewAccessLogEntry);
		}

		public void AddTapGesture()
		{
			var tap = new UITapGestureRecognizer ();
			tap.AddTarget (ToggleControls);
			tap.NumberOfTapsRequired = 1;
			View.AddGestureRecognizer (tap);
		}

		public override void ObserveValue (NSString keyPath, NSObject ofObject, NSDictionary change, IntPtr context)
		{
			if (context == StatusObservationContext.Handle) {
				var playerItem = ofObject as AVPlayerItem;
				if (playerItem.Status == AVPlayerItemStatus.ReadyToPlay) {
					Console.WriteLine ("ReadyToPlay");
				} else if (playerItem.Status == AVPlayerItemStatus.Failed) {
					Console.WriteLine ("Failed");
				}
			}else if(context == PlaybackLikelyToKeepUpContext.Handle){
				Console.WriteLine ("PlaybackLikelyToKeepUpContext");
			}else if(context == PlaybackBufferFullContext.Handle){
				Console.WriteLine ("PlaybackBufferFullContext");
			}else if(context == PlaybackBufferEmptyContext.Handle){
				Console.WriteLine ("PlaybackBufferEmptyContext");
			}else {
				base.ObserveValue (keyPath, ofObject, change, context);
			}
		}
        
		void ToggleControls()
		{
			if (ControlsView.Hidden)
				ShowToolbar ();
			else
				HideToolbar (null, null);

			if(touchTimer != null)
				touchTimer.Stop ();

			touchTimer = new Timer(3000);
			touchTimer.Elapsed += new ElapsedEventHandler(HideToolbar);
			touchTimer.Enabled = true;
		}

		void ShowToolbar()
		{
			ControlsView.Hidden = false;

			InvokeOnMainThread (delegate {
				UIView.Animate (
					0, 0, UIViewAnimationOptions.CurveEaseIn,
					() => {
						ControlsView.Alpha = 1f;
					}, () => {
					});
			});
		}

		void HideToolbar(object sender, ElapsedEventArgs e)
		{
			if (!IsPlaying() || IsScrubbing())
				return;

			if(touchTimer != null)
				touchTimer.Stop ();

			InvokeOnMainThread (delegate {
				UIView.Animate (
					0.4, 0, UIViewAnimationOptions.CurveEaseOut,
					() => {
						ControlsView.Alpha = 0f;
					}, () => {
						ControlsView.Hidden = true;
					});
			});
		}

		void InitScrubberTimer()
		{
			CMTime playerDuration = _playerItem.Duration;
			if (playerDuration == CMTime.Invalid)
				return;

			TimeObserver = _player.AddPeriodicTimeObserver (CMTime.FromSeconds(1.0/60.0, NSEC_PER_SEC), null, (CMTime obj) => {
				SyncScrubber ();
			});
		}

		void SyncScrubber()
		{
			CMTime playerDuration = _playerItem.Duration;
			if (playerDuration == CMTime.Invalid) 
			{
				Scrubber.MinValue = 0.0f;
				return;
			}

			double duration = _playerItem.Duration.Seconds;
			if(!double.IsInfinity(duration) && !double.IsNaN(duration))
			{
				float minValue = Scrubber.MinValue;
				float maxValue = Scrubber.MaxValue;

				double time = _playerItem.CurrentTime.Seconds;

				float value = (float)((maxValue - minValue) * time / duration + minValue);
				Scrubber.SetValue (value, true);

				/*TODO  We can update time labels here */
			}
		}

		partial void BeginScrubbing (NSObject sender)
		{
			RestoreAfterScrubbingRate = _player.Rate;
			_player.Rate = 0f;

			RemovePlayerTimeObserver();
		}

		partial void Scrub (NSObject sender)
		{
			if(sender.GetType() == typeof(UISlider) && !IsSeeking)
			{
				IsSeeking = true;
				UISlider slider = (UISlider)sender;

				CMTime playerDuration = _playerItem.Duration;
				if(playerDuration == CMTime.Invalid){
					return;
				}

				double duration = playerDuration.Seconds;
				if(!double.IsInfinity(duration) && !double.IsNaN(duration))
				{
					float minValue = slider.MinValue;
					float maxValue = slider.MaxValue;
					float value = slider.Value;

					double time = duration * (value - minValue) / (maxValue - minValue);

					_player.Seek(CMTime.FromSeconds(time, NSEC_PER_SEC), async delegate(bool finished) {
						IsSeeking = false;	
					});

					/*TODO  We can update time labels here */
				}
			}
		}

		partial void EndScrubbing (NSObject sender)
		{
			if(TimeObserver == null)
			{
				CMTime playerDuration = _playerItem.Duration;
				if(playerDuration == CMTime.Invalid){
					return;
				}

				double duration = playerDuration.Seconds;
				if(!double.IsInfinity(duration) && !double.IsNaN(duration))
				{
					TimeObserver = _player.AddPeriodicTimeObserver (CMTime.FromSeconds(1.0/60.0, NSEC_PER_SEC), null, (CMTime obj) => {
						SyncScrubber ();
					});
				}
			}

			if(RestoreAfterScrubbingRate != 0f)
			{
				_player.Rate = RestoreAfterScrubbingRate;
				RestoreAfterScrubbingRate = 0f;
			}
		}
		partial void TogglePlay (NSObject sender)
		{
			var isplayer = IsPlaying();
			Console.WriteLine("Playing: " + isplayer);

			if(isplayer){
				_player.Pause ();
				PlayButton.SetTitle("Play", UIControlState.Normal);
			}else{
				_player.Play ();
				PlayButton.SetTitle("Pause", UIControlState.Normal);
			}
		}

		partial void ToggleFullscreen (NSObject sender)
		{
			/*TODO*/
		}

		bool IsPlaying()
		{
			return RestoreAfterScrubbingRate != 0f || _player.Rate != 0f;
		}

		bool IsScrubbing()
		{
			return RestoreAfterScrubbingRate != 0f;
		}

		void RemovePlayerTimeObserver()
		{
			if (TimeObserver != null) 
			{
				_player.RemoveTimeObserver (TimeObserver);
				TimeObserver.Dispose ();
				TimeObserver = null;
			}
		}

		void AVPlayerItemPlaybackStalled(NSNotification notification)
		{
			Console.WriteLine ("AVPlayerItemPlaybackStalled!");
		}

		void AVPlayerItemNewErrorLogEntry(NSNotification notification)
		{
			Console.WriteLine ("AVPlayerItemNewErrorLogEntry");
		}

		void AVPlayerItemNewAccessLogEntry(NSNotification notification)
		{
			Console.WriteLine ("AVPlayerItemNewAccessLogEntry");
		}

		void PlayerPlaybackError(NSNotification notification)
		{
			Console.WriteLine ("PlayerPlaybackError");
		}

		void PlayerPlaybackDidFinish(NSNotification notification)
		{
			Console.WriteLine("PlayerPlaybackDidFinish");
		}

        public override void ViewDidUnload ()
        {
            base.ViewDidUnload ();
            
			UnloadNotifications ();

			ReleasePlayerItem ();
            // Clear any references to subviews of the main view in order to
            // allow the Garbage Collector to collect them sooner.
            //
            // e.g. myOutlet.Dispose (); myOutlet = null;
            
            ReleaseDesignerOutlets ();
        }
        
        public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
        {
            // Return true for supported orientations
            return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
        }


		void UnloadNotifications()
		{
			if (_aVPlayerItemFailedToPlayToEndTimeNotificationObserver != null)
				NSNotificationCenter.DefaultCenter.RemoveObserver(_aVPlayerItemFailedToPlayToEndTimeNotificationObserver);

			if (_aVPlayerItemDidPlayToEndTimeNotificationObserver != null)
				NSNotificationCenter.DefaultCenter.RemoveObserver(_aVPlayerItemDidPlayToEndTimeNotificationObserver);

			if (_aVPlayerItemPlaybackStalledNotificationObserver != null)
				NSNotificationCenter.DefaultCenter.RemoveObserver(_aVPlayerItemPlaybackStalledNotificationObserver);

			if (_aVPlayerItemNewErrorLogEntryNotificationObserver != null)
				NSNotificationCenter.DefaultCenter.RemoveObserver(_aVPlayerItemNewErrorLogEntryNotificationObserver);

			if (_aVPlayerItemNewAccessLogEntryNotificationObserver != null)
				NSNotificationCenter.DefaultCenter.RemoveObserver(_aVPlayerItemNewAccessLogEntryNotificationObserver);
		}
    }
}

