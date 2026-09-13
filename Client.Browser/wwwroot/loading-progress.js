// Only the moving portion is HTML. The game keeps its existing bitmap text,
// frame, and determinate download progress, without a second loading dialog.
window.OpenGarrisonLoadingProgress = (() => {
    let track;
    let previousBounds;
    function show(left, top, width, height) {
        if (!track) {
            const shell = document.querySelector('.game-shell');
            if (!shell) return;
            track = document.createElement('div');
            track.className = 'game-loading-progress';
            track.setAttribute('role', 'progressbar');
            track.setAttribute('aria-label', 'Loading');
            const runner = document.createElement('div');
            runner.className = 'game-loading-progress-runner';
            track.appendChild(runner);
            shell.appendChild(track);
        }
        const bounds = [left, top, width, height].join(',');
        if (bounds !== previousBounds) {
            track.style.left = `${left * 100}%`;
            track.style.top = `${top * 100}%`;
            track.style.width = `${width * 100}%`;
            track.style.height = `${height * 100}%`;
            previousBounds = bounds;
        }
        track.hidden = false;
    }
    function hide() {
        if (track && !track.hidden) track.hidden = true;
    }
    return { show, hide };
})();
