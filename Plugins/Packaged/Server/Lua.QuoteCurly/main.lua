local plugin = {}

function plugin.initialize(host)
    plugin.host = host
    host.log("Quote/Curly gameplay pack is registered from plugin-owned JSON.")
end

return plugin
