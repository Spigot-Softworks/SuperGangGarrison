
linux/macOS
- if extracted app files are not marked executable on first launch, run `sh run-client.sh`, `sh run-server.sh`, or `sh run-server-launcher.sh`
- the helper scripts will fix the executable bit for the packaged binaries in place and then start them
- linux audio uses the system OpenAL library; if audio is unavailable the client will continue with sound disabled

config files
- config/OpenGarrison.ini
- config/controls.OpenGarrison
- config/sampleMapRotation.txt
