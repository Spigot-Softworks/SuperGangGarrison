import struct
import unittest

from gg2_lobby import LIST_REQUEST, PROTOCOL_ID, parse_server_block


def field(key: str, value: bytes) -> bytes:
    key_bytes = key.encode()
    return bytes([len(key_bytes)]) + key_bytes + struct.pack(">H", len(value)) + value


class Gg2LobbyTests(unittest.TestCase):
    def test_parses_native_lobby_advertisement(self):
        fields = [
            field("name", b"Vindicator's"),
            field("map", b"koth_corinth"),
            field("game_ver", b"2.9.2"),
            field("protocol_id", PROTOCOL_ID),
        ]
        block = (
            b"\0" + struct.pack(">H", 8190) + bytes([45, 59, 102, 99])
            + bytes(18) + struct.pack(">HHHHH", 10, 3, 1, 0, len(fields))
            + b"".join(fields)
        )
        server = parse_server_block(block)
        self.assertIsNotNone(server)
        self.assertEqual(server["name"], "Vindicator's")
        self.assertEqual(server["host"], "45.59.102.99")
        self.assertEqual(server["map"], "koth_corinth")
        self.assertTrue(server["isCompatible"])
        self.assertIsNone(parse_server_block(block[:12]))
        self.assertEqual(len(LIST_REQUEST), 32)


if __name__ == "__main__":
    unittest.main()
