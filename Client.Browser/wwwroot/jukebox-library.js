(() => {
    "use strict";
    const MAX_TRACK = 32 * 1024 * 1024, MAX_LIBRARY = 256 * 1024 * 1024;
    let database, dialog;
    async function db() {
        if (database) return database;
        database = await new Promise((resolve, reject) => {
            const request = indexedDB.open("OpenGarrisonMusic", 1);
            request.onupgradeneeded = () => request.result.createObjectStore("tracks", { keyPath: "name" });
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
        return database;
    }
    async function transaction(mode, action) {
        const connection = await db();
        return new Promise((resolve, reject) => {
            const tx = connection.transaction("tracks", mode);
            const request = action(tx.objectStore("tracks"));
            tx.oncomplete = () => resolve(request.result);
            tx.onabort = tx.onerror = () => reject(tx.error || new Error("Music storage failed."));
        });
    }
    const all = () => transaction("readonly", store => store.getAll());
    window.OpenGarrisonJukebox = {
        async restore(callback) {
            const tracks = await all();
            if (tracks.reduce((sum, track) => sum + track.blob.size, 0) > MAX_LIBRARY) throw new Error("Music library exceeds 256 MB.");
            for (const track of tracks) {
                if (track.blob.size > MAX_TRACK) throw new Error("Track exceeds 32 MB.");
                await callback.invokeMethodAsync("ReceiveTrack", track.name, new Uint8Array(await track.blob.arrayBuffer()));
            }
            return tracks.map(track => track.name);
        },
        async manage() {
            if (dialog) { dialog.showModal(); return; }
            dialog = document.createElement("dialog");
            Object.assign(dialog.style, { background: "#22201e", color: "white", border: "2px solid #a33", borderRadius: "8px",
                width: "min(520px, 80vw)", maxHeight: "75vh", font: "16px system-ui", zIndex: "2147483647" });
            const heading = document.createElement("h2"); heading.textContent = "Music Library"; dialog.append(heading);
            const help = document.createElement("p");
            help.textContent = "Import WAV, MP3 or OGG tracks. They stay in this browser's storage between visits. Clearing site data removes them. Maximum: 32 MB per track, 256 MB total.";
            dialog.append(help);
            const status = document.createElement("p"); status.setAttribute("role", "status");
            const picker = document.createElement("input"); picker.type = "file"; picker.multiple = true; picker.accept = ".wav,.mp3,.ogg";
            picker.setAttribute("aria-label", "Import music tracks"); dialog.append(picker, status);
            const list = document.createElement("div"); dialog.append(list);
            const done = document.createElement("button"); done.textContent = "Done"; done.onclick = () => dialog.close(); dialog.append(done);
            dialog.addEventListener("keydown", event => event.stopPropagation());
            dialog.addEventListener("keyup", event => event.stopPropagation());
            dialog.addEventListener("close", () => { dialog.remove(); dialog = null; });
            document.body.append(dialog); dialog.showModal();
            async function refresh() {
                list.replaceChildren();
                for (const track of await all()) {
                    const row = document.createElement("p"), label = document.createElement("span"), remove = document.createElement("button");
                    label.textContent = track.name + " "; remove.textContent = "Remove";
                    remove.onclick = async () => {
                        try { await transaction("readwrite", store => store.delete(track.name)); await refresh(); }
                        catch (error) { status.textContent = error.message; }
                    };
                    row.append(label, remove); list.append(row);
                }
            }
            picker.onchange = async () => {
                picker.disabled = true;
                try {
                    for (const file of picker.files) {
                        if (!/\.(wav|mp3|ogg)$/i.test(file.name) || /[\\/:\r\n]/.test(file.name) || file.name.length > 100 || new TextEncoder().encode(file.name).length > 160)
                            throw new Error("Use WAV, MP3 or OGG files with names under 100 characters.");
                        if (file.size > MAX_TRACK) throw new Error(file.name + " exceeds 32 MB.");
                        const used = (await all()).filter(track => track.name !== file.name).reduce((sum, track) => sum + track.blob.size, 0);
                        if (used + file.size > MAX_LIBRARY) throw new Error("Library is full. Remove a track before importing more.");
                        await transaction("readwrite", store => store.put({ name: file.name, blob: file }));
                    }
                    status.textContent = "Saved. Use Refresh Tracks in the jukebox to load your library.";
                    await refresh();
                } catch (error) { status.textContent = error.name === "QuotaExceededError" ? "Browser storage is full. Remove tracks to free space." : error.message; }
                finally { picker.disabled = false; picker.value = ""; }
            };
            try { await refresh(); } catch (error) { status.textContent = "Music storage is unavailable: " + error.message; }
        }
    };
})();
