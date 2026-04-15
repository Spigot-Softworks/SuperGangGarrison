<?php
declare(strict_types=1);

const REGISTRY_WRITE_TOKEN = 'change-me';
const REGISTRY_DATA_DIR = __DIR__ . '/og2servers-data';
const REGISTRY_DATA_FILE = REGISTRY_DATA_DIR . '/servers.json';
const REGISTRY_STALE_SECONDS = 120;
const REGISTRY_MAX_NAME_BYTES = 80;
const REGISTRY_MAX_HOST_BYTES = 255;
const REGISTRY_MAX_WEBSOCKET_URL_BYTES = 512;
const REGISTRY_MAX_MAP_BYTES = 80;
const REGISTRY_MAX_MODE_BYTES = 40;
const REGISTRY_MAX_SERVERS_PER_IP = 8;
const REGISTRY_MIN_HEARTBEAT_SECONDS = 10;

send_common_headers();

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    http_response_code(204);
    exit;
}

try {
    if ($_SERVER['REQUEST_METHOD'] === 'GET') {
        handle_get_servers();
        exit;
    }

    if ($_SERVER['REQUEST_METHOD'] === 'POST') {
        handle_post_heartbeat();
        exit;
    }

    send_json(['error' => 'method_not_allowed'], 405);
} catch (Throwable $error) {
    send_json(['error' => 'server_error'], 500);
}

function handle_get_servers(): void
{
    $servers = load_servers();
    $servers = remove_stale_servers($servers, time());
    save_servers($servers);

    $publicServers = array_values(array_map('to_public_server', $servers));
    usort($publicServers, static function (array $left, array $right): int {
        return strcasecmp((string)($left['name'] ?? ''), (string)($right['name'] ?? ''));
    });

    send_json([
        'servers' => $publicServers,
        'generatedAt' => gmdate(DATE_ATOM),
    ]);
}

function handle_post_heartbeat(): void
{
    $input = read_input();
    $token = normalize_string($input['token'] ?? '', 256);
    $hasAdminToken = is_write_token_configured() && hash_equals(REGISTRY_WRITE_TOKEN, $token);
    $remoteAddress = normalize_host((string)($_SERVER['REMOTE_ADDR'] ?? ''));

    $action = strtolower(normalize_string($input['action'] ?? 'heartbeat', 20));
    $servers = remove_stale_servers(load_servers(), time());

    if ($action === 'remove') {
        if (!$hasAdminToken) {
            send_json(['error' => 'unauthorized'], 401);
            return;
        }

        $serverId = normalize_server_id((string)($input['serverId'] ?? ''));
        if ($serverId === '') {
            send_json(['error' => 'server_id_required'], 400);
            return;
        }

        unset($servers[$serverId]);
        save_servers($servers);
        send_json(['ok' => true, 'removed' => true]);
        return;
    }

    if ($action !== 'heartbeat') {
        send_json(['error' => 'invalid_action'], 400);
        return;
    }

    $host = normalize_host((string)($input['host'] ?? ''));
    if ($host === '') {
        $host = $remoteAddress;
    } elseif (!$hasAdminToken && !host_matches_remote_address($host, $remoteAddress)) {
        send_json(['error' => 'host_does_not_resolve_to_request_ip'], 403);
        return;
    }

    $udpPort = normalize_port($input['udpPort'] ?? 0);
    $webSocketPort = normalize_port($input['webSocketPort'] ?? 0);
    $webSocketUrl = normalize_websocket_url($input['webSocketUrl'] ?? '');
    if ($webSocketUrl !== '' && !$hasAdminToken && !websocket_url_matches_host($webSocketUrl, $host, $remoteAddress)) {
        send_json(['error' => 'websocket_url_host_not_allowed'], 403);
        return;
    }

    if ($host === '' || ($udpPort === 0 && $webSocketPort === 0 && $webSocketUrl === '')) {
        send_json(['error' => 'valid_host_and_port_required'], 400);
        return;
    }

    $serverId = hash('sha256', strtolower($host) . ':' . $udpPort . ':' . $webSocketPort . ':' . strtolower($webSocketUrl));
    if (!$hasAdminToken && !rate_limit_allows_heartbeat($servers, $remoteAddress, $serverId)) {
        send_json(['error' => 'rate_limited'], 429);
        return;
    }

    $now = time();
    $servers[$serverId] = [
        'serverId' => $serverId,
        'name' => normalize_string($input['name'] ?? $host, REGISTRY_MAX_NAME_BYTES),
        'host' => $host,
        'udpPort' => $udpPort,
        'webSocketPort' => $webSocketPort,
        'webSocketUrl' => $webSocketUrl,
        'private' => normalize_bool($input['private'] ?? false),
        'map' => normalize_string($input['map'] ?? '', REGISTRY_MAX_MAP_BYTES),
        'mode' => normalize_string($input['mode'] ?? '', REGISTRY_MAX_MODE_BYTES),
        'players' => normalize_non_negative_int($input['players'] ?? 0),
        'maxPlayers' => normalize_non_negative_int($input['maxPlayers'] ?? 0),
        'spectators' => normalize_non_negative_int($input['spectators'] ?? 0),
        'protocolVersion' => normalize_non_negative_int($input['protocolVersion'] ?? 0),
        'remoteAddress' => $remoteAddress,
        'lastSeen' => $now,
        'lastSeenIso' => gmdate(DATE_ATOM, $now),
    ];

    save_servers($servers);
    send_json([
        'ok' => true,
        'serverId' => $serverId,
        'expiresInSeconds' => REGISTRY_STALE_SECONDS,
    ]);
}

function read_input(): array
{
    $raw = file_get_contents('php://input');
    if (is_string($raw) && trim($raw) !== '') {
        $decoded = json_decode($raw, true);
        if (is_array($decoded)) {
            return $decoded;
        }
    }

    return $_POST;
}

function load_servers(): array
{
    ensure_data_dir();
    if (!is_file(REGISTRY_DATA_FILE)) {
        return [];
    }

    $raw = file_get_contents(REGISTRY_DATA_FILE);
    if (!is_string($raw) || trim($raw) === '') {
        return [];
    }

    $decoded = json_decode($raw, true);
    return is_array($decoded) ? $decoded : [];
}

function save_servers(array $servers): void
{
    ensure_data_dir();
    $lockFile = REGISTRY_DATA_FILE . '.lock';
    $lockHandle = fopen($lockFile, 'c');
    if ($lockHandle === false) {
        throw new RuntimeException('lock_open_failed');
    }

    try {
        if (!flock($lockHandle, LOCK_EX)) {
            throw new RuntimeException('lock_failed');
        }

        $json = json_encode($servers, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
        if (!is_string($json)) {
            throw new RuntimeException('json_encode_failed');
        }

        file_put_contents(REGISTRY_DATA_FILE, $json . PHP_EOL, LOCK_EX);
        flock($lockHandle, LOCK_UN);
    } finally {
        fclose($lockHandle);
    }
}

function ensure_data_dir(): void
{
    if (!is_dir(REGISTRY_DATA_DIR) && !mkdir(REGISTRY_DATA_DIR, 0755, true) && !is_dir(REGISTRY_DATA_DIR)) {
        throw new RuntimeException('data_dir_create_failed');
    }

    $htaccessPath = REGISTRY_DATA_DIR . '/.htaccess';
    if (!is_file($htaccessPath)) {
        file_put_contents($htaccessPath, "Require all denied\nDeny from all\n");
    }
}

function remove_stale_servers(array $servers, int $now): array
{
    foreach ($servers as $serverId => $server) {
        $lastSeen = isset($server['lastSeen']) ? (int)$server['lastSeen'] : 0;
        if ($lastSeen <= 0 || ($now - $lastSeen) > REGISTRY_STALE_SECONDS) {
            unset($servers[$serverId]);
        }
    }

    return $servers;
}

function rate_limit_allows_heartbeat(array $servers, string $remoteAddress, string $serverId): bool
{
    $now = time();
    $countForIp = 0;
    foreach ($servers as $existingServerId => $server) {
        if ((string)($server['remoteAddress'] ?? '') !== $remoteAddress) {
            continue;
        }

        if ($existingServerId !== $serverId) {
            $countForIp += 1;
            continue;
        }

        $lastSeen = isset($server['lastSeen']) ? (int)$server['lastSeen'] : 0;
        if ($lastSeen > 0 && ($now - $lastSeen) < REGISTRY_MIN_HEARTBEAT_SECONDS) {
            return false;
        }
    }

    return $countForIp < REGISTRY_MAX_SERVERS_PER_IP;
}

function host_matches_remote_address(string $host, string $remoteAddress): bool
{
    if ($host === '' || $remoteAddress === '') {
        return false;
    }

    if (strcasecmp($host, $remoteAddress) === 0) {
        return true;
    }

    $remotePacked = @inet_pton($remoteAddress);
    if ($remotePacked === false) {
        return false;
    }

    $records = @dns_get_record($host, DNS_A + DNS_AAAA);
    if (!is_array($records)) {
        return false;
    }

    foreach ($records as $record) {
        $address = (string)($record['ip'] ?? $record['ipv6'] ?? '');
        $packed = @inet_pton($address);
        if ($packed !== false && hash_equals($remotePacked, $packed)) {
            return true;
        }
    }

    return false;
}

function to_public_server(array $server): array
{
    return [
        'name' => (string)($server['name'] ?? ''),
        'host' => (string)($server['host'] ?? ''),
        'udpPort' => (int)($server['udpPort'] ?? 0),
        'webSocketPort' => (int)($server['webSocketPort'] ?? 0),
        'webSocketUrl' => (string)($server['webSocketUrl'] ?? ''),
        'private' => (bool)($server['private'] ?? false),
        'map' => (string)($server['map'] ?? ''),
        'mode' => (string)($server['mode'] ?? ''),
        'players' => (int)($server['players'] ?? 0),
        'maxPlayers' => (int)($server['maxPlayers'] ?? 0),
        'spectators' => (int)($server['spectators'] ?? 0),
        'protocolVersion' => (int)($server['protocolVersion'] ?? 0),
        'lastSeenIso' => (string)($server['lastSeenIso'] ?? ''),
    ];
}

function normalize_string(mixed $value, int $maxBytes): string
{
    $text = is_scalar($value) ? trim((string)$value) : '';
    $text = str_replace(["\r", "\n", "\0"], ' ', $text);
    if (strlen($text) > $maxBytes) {
        $text = substr($text, 0, $maxBytes);
    }

    return $text;
}

function normalize_host(string $host): string
{
    $host = normalize_string($host, REGISTRY_MAX_HOST_BYTES);
    if ($host === '') {
        return '';
    }

    if (str_contains($host, '://') || str_contains($host, '/')) {
        return '';
    }

    return preg_match('/^[A-Za-z0-9_.:-]+$/', $host) === 1 ? $host : '';
}

function normalize_websocket_url(mixed $value): string
{
    $url = normalize_string($value, REGISTRY_MAX_WEBSOCKET_URL_BYTES);
    if ($url === '') {
        return '';
    }

    $parts = parse_url($url);
    if (!is_array($parts)) {
        return '';
    }

    $scheme = strtolower((string)($parts['scheme'] ?? ''));
    $host = (string)($parts['host'] ?? '');
    if (($scheme !== 'ws' && $scheme !== 'wss') || normalize_host($host) === '') {
        return '';
    }

    if (isset($parts['user']) || isset($parts['pass']) || isset($parts['fragment'])) {
        return '';
    }

    $port = isset($parts['port']) ? normalize_port($parts['port']) : 0;
    if (isset($parts['port']) && $port === 0) {
        return '';
    }

    $path = (string)($parts['path'] ?? '/opengarrison/ws');
    if ($path === '') {
        $path = '/opengarrison/ws';
    }

    $query = isset($parts['query']) ? '?' . (string)$parts['query'] : '';
    return $scheme . '://' . $host . ($port > 0 ? ':' . $port : '') . $path . $query;
}

function websocket_url_matches_host(string $webSocketUrl, string $host, string $remoteAddress): bool
{
    $parts = parse_url($webSocketUrl);
    if (!is_array($parts)) {
        return false;
    }

    $urlHost = normalize_host((string)($parts['host'] ?? ''));
    if ($urlHost === '') {
        return false;
    }

    return strcasecmp($urlHost, $host) === 0 || host_matches_remote_address($urlHost, $remoteAddress);
}

function normalize_server_id(string $serverId): string
{
    $serverId = normalize_string($serverId, 96);
    return preg_match('/^[A-Za-z0-9_.:-]+$/', $serverId) === 1 ? $serverId : '';
}

function normalize_port(mixed $value): int
{
    $port = is_numeric($value) ? (int)$value : 0;
    return $port > 0 && $port <= 65535 ? $port : 0;
}

function normalize_non_negative_int(mixed $value): int
{
    $number = is_numeric($value) ? (int)$value : 0;
    return max(0, $number);
}

function normalize_bool(mixed $value): bool
{
    if (is_bool($value)) {
        return $value;
    }

    if (is_numeric($value)) {
        return (int)$value !== 0;
    }

    if (is_string($value)) {
        return in_array(strtolower(trim($value)), ['1', 'true', 'yes', 'on'], true);
    }

    return false;
}

function is_write_token_configured(): bool
{
    return REGISTRY_WRITE_TOKEN !== '' && REGISTRY_WRITE_TOKEN !== 'change-me';
}

function send_common_headers(): void
{
    header('Access-Control-Allow-Origin: *');
    header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
    header('Access-Control-Allow-Headers: Content-Type');
    header('Cache-Control: no-store, max-age=0');
}

function send_json(array $payload, int $status = 200): void
{
    http_response_code($status);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode($payload, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES) . PHP_EOL;
}
