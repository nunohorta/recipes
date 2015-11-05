// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace AVPlayerDemo
{
	[Register ("AVPlayerDemoViewController")]
	partial class AVPlayerDemoViewController
	{
		[Outlet]
		UIKit.UIView ControlsView { get; set; }

		[Outlet]
		UIKit.UIButton FullscreenButton { get; set; }

		[Outlet]
		UIKit.UIButton PlayButton { get; set; }

		[Outlet]
		UIKit.UISlider Scrubber { get; set; }

		[Action ("BeginScrubbing:")]
		partial void BeginScrubbing (Foundation.NSObject sender);

		[Action ("EndScrubbing:")]
		partial void EndScrubbing (Foundation.NSObject sender);

		[Action ("Play:")]
		partial void Play (Foundation.NSObject sender);

		[Action ("Scrub:")]
		partial void Scrub (Foundation.NSObject sender);

		[Action ("ToggleFullscreen:")]
		partial void ToggleFullscreen (Foundation.NSObject sender);

		[Action ("TogglePlay:")]
		partial void TogglePlay (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (ControlsView != null) {
				ControlsView.Dispose ();
				ControlsView = null;
			}

			if (PlayButton != null) {
				PlayButton.Dispose ();
				PlayButton = null;
			}

			if (Scrubber != null) {
				Scrubber.Dispose ();
				Scrubber = null;
			}

			if (FullscreenButton != null) {
				FullscreenButton.Dispose ();
				FullscreenButton = null;
			}
		}
	}
}
