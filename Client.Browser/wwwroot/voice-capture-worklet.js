class VoiceCaptureProcessor extends AudioWorkletProcessor {
    constructor() {
        super();
        this.samples = new Int16Array(960);
        this.written = 0;
    }
    process(inputs) {
        const input = inputs[0]?.[0];
        if (!input) return true;
        for (const sample of input) {
            this.samples[this.written++] = Math.max(-32768, Math.min(32767, sample * 32767));
            if (this.written === this.samples.length) {
                this.port.postMessage(this.samples.buffer, [this.samples.buffer]);
                this.samples = new Int16Array(960);
                this.written = 0;
            }
        }
        return true;
    }
}
registerProcessor("opengarrison-voice-capture", VoiceCaptureProcessor);
