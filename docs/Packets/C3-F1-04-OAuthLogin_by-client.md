# C3 F1 04 - OAuthLogin (by client)

## Is sent when

The player tries to log into the game using OAuth.

## Causes the following actions on the server side

The server is authenticating the token. If it's correct, the state of the player is proceeding to be logged in.

## Structure

| Index | Length | Data Type | Value | Description |
|-------|--------|-----------|-------|-------------|
| 0 | 1 |   Byte   | 0xC3  | [Packet type](PacketTypes.md) |
| 1 | 1 |    Byte   |   0   | Packet header - length of the packet |
| 2 | 1 |    Byte   | 0xF1  | Packet header - packet type identifier |
| 3 | 1 |    Byte   | 0x04  | Packet header - sub packet type identifier |
| 4 | 1 | Byte |  | Provider; 0=Google, 1=Facebook, 2=Apple |
| 5 |  | String |  | Token; The OAuth token. |