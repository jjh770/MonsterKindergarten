mergeInto(LibraryManager.library, {
    RegisterVisibilityChangeEvent: function () {
        document.addEventListener("visibilitychange", function () {
            var paused = document.hidden ? 1 : 0;
            SendMessage("AudioManager", "OnBrowserPause", paused);
        });
    }
});
