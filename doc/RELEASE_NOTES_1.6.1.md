# MiaDock 1.6.1.0 release notes

* Real-time audio spectrum is now experimental, opt-in, and disabled by default.
* The stable visualizer uses the selected media session's real peak amplitude without starting spectrum capture resources.
* When experimental spectrum is off and real amplitude is unavailable, the legacy centered three-bar animation remains active.
* Enabling spectrum uses ProcessSpectrum, EndpointSpectrum, SessionPeak, and Unavailable in order, with bounded retry behavior.
* Live setting changes safely stop or restart eligible capture resources without requiring an app restart.
