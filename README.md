PlaySheet
=========

PlaySheet (for lack of a better project name) is a media player
for Windows built using F# and WPF.

It uses [MPV](https://mpv.io/) for rendering, and builds its
UI on top of it. It is designed to be minimal, and easy to extend.  
In fact, the core application only starts MPV and provides many APIs for
extensions. However, there is no built-in UI or keyboard bindings (those
are implemented in [PlaySheet.DefaultUI](./PlaySheet.DefaultUI), which is
optional).

Key bindings, UIs, gestures, additional windows and configuration
loading / saving / refreshing is handled by the core application.

**Development on PlaySheet has been indeterminately paused, as
I have started to work on other things. Should anyone want to
contribute or start their own fork, they should send me a message
or file an issue so that I can explain the structure of the repository
better.**
