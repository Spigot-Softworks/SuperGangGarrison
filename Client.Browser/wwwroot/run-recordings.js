(() => {
    async function database() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open("og2-run-recordings", 1);
            request.onupgradeneeded = () => request.result.createObjectStore("pending");
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }
    async function transact(mode, operation) {
        const db = await database();
        try {
            return await new Promise((resolve, reject) => {
                const tx = db.transaction("pending", mode);
                const request = operation(tx.objectStore("pending"));
                tx.oncomplete = () => resolve(request.result);
                tx.onerror = () => reject(tx.error);
                tx.onabort = () => reject(tx.error || new Error("Run storage transaction aborted"));
            });
        } finally { db.close(); }
    }
    window.OpenGarrisonRunRecordings = {
        load: async () => "[" + (await transact("readonly", store => store.getAll())).join(",") + "]",
        save: (id, json) => transact("readwrite", store => store.put(json, id)),
        remove: id => transact("readwrite", store => store.delete(id))
    };
})();
