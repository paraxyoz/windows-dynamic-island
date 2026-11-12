"""Read-only bridge for an application approved for Discord local RPC."""
import json
import os
import queue
import re
import struct
import sys
import threading
import uuid

from island_ipc import send

MAX_FRAME = 1024 * 1024


def read_exact(pipe, count):
    data = bytearray()
    while len(data) < count:
        chunk = pipe.read(count - len(data))
        if not chunk:
            raise EOFError("Discord closed the connection")
        data.extend(chunk)
    return bytes(data)


def write_frame(pipe, opcode, packet):
    data = json.dumps(packet, ensure_ascii=False).encode("utf-8")
    pipe.write(struct.pack("<II", opcode, len(data)) + data)


def read_frame(pipe):
    opcode, size = struct.unpack("<II", read_exact(pipe, 8))
    if size > MAX_FRAME:
        raise ValueError("Discord RPC frame exceeds limit")
    return opcode, json.loads(read_exact(pipe, size)) if size else {}


def connect():
    for index in range(10):
        try:
            return open(fr"\\.\pipe\discord-ipc-{index}", "r+b", buffering=0)
        except OSError:
            continue
    raise RuntimeError("Не найден локальный RPC Discord Desktop")


def sanitize(value, limit):
    return " ".join(str(value or "").replace("|", " ").split())[:limit]


def main():
    client = os.environ.get("ISLAND_DISCORD_CLIENT_ID", "")
    token = os.environ.get("ISLAND_DISCORD_RPC_TOKEN", "")
    if not re.fullmatch(r"\d{17,22}", client) or not token:
        raise RuntimeError("Нужен ID одобренного приложения и OAuth-токен Discord RPC в окружении")
    with connect() as pipe:
        packets = queue.Queue(maxsize=64)

        def reader():
            try:
                while True:
                    packets.put(read_frame(pipe))
            except (OSError, ValueError, EOFError) as error:
                packets.put(error)

        threading.Thread(target=reader, daemon=True).start()
        write_frame(pipe, 0, {"v": 1, "client_id": client})

        def request(cmd, args=None, event=None):
            packet = {"cmd": cmd, "nonce": uuid.uuid4().hex}
            if args is not None:
                packet["args"] = args
            if event is not None:
                packet["evt"] = event
            write_frame(pipe, 1, packet)

        def receive(timeout):
            value = packets.get(timeout=timeout)
            if isinstance(value, Exception):
                raise value
            return value

        opcode, hello = receive(10)
        if opcode == 2 or hello.get("evt") != "READY":
            raise RuntimeError("Discord RPC не подтвердил подключение")
        request("AUTHENTICATE", {"access_token": token})
        opcode, auth = receive(10)
        if opcode == 2 or auth.get("cmd") != "AUTHENTICATE" or auth.get("evt") == "ERROR":
            raise RuntimeError("Discord отклонил OAuth-токен")
        scopes = set(auth.get("data", {}).get("scopes", []))
        voice = "rpc.voice.read" in scopes
        notifications = "rpc.notifications.read" in scopes
        if not voice and not notifications:
            raise RuntimeError("Discord не предоставил доступ к голосовым настройкам или уведомлениям")
        if notifications:
            request("SUBSCRIBE", {}, "NOTIFICATION_CREATE")
        if voice:
            request("SUBSCRIBE", {}, "VOICE_SETTINGS_UPDATE")
            request("SUBSCRIBE", {}, "VOICE_CHANNEL_SELECT")
        channel = ""
        muted = None

        def update_voice():
            if voice:
                state = "unknown" if muted is None else "muted" if muted else "unmuted"
                send(f"DISCORD:voice|{state}|{channel}")

        def refresh():
            if voice:
                request("GET_VOICE_SETTINGS")
                request("GET_SELECTED_VOICE_CHANNEL")

        refresh()
        print("Мост Discord подключён. Ctrl+C — выход.")
        import time
        deadline = time.monotonic() + 8
        while True:
            try:
                opcode, packet = receive(max(0.05, deadline - time.monotonic()))
            except queue.Empty:
                refresh()
                deadline = time.monotonic() + 8
                continue
            if opcode == 2:
                raise RuntimeError("Discord завершил RPC-сеанс")
            if opcode == 3:
                write_frame(pipe, 4, packet)
                continue
            event, cmd = packet.get("evt"), packet.get("cmd")
            if event == "ERROR":
                print(f"Discord отклонил команду {cmd or 'RPC'}.", file=sys.stderr)
                continue
            data = packet.get("data") or {}
            if cmd == "GET_SELECTED_VOICE_CHANNEL":
                channel = sanitize(data.get("name"), 100)
                update_voice()
            elif cmd == "GET_VOICE_SETTINGS" or event == "VOICE_SETTINGS_UPDATE":
                muted = data.get("mute") if isinstance(data.get("mute"), bool) else None
                update_voice()
            elif event == "VOICE_CHANNEL_SELECT":
                refresh()
            elif event == "NOTIFICATION_CREATE":
                message = data.get("message") or {}
                title = sanitize(data.get("title"), 110)
                body = sanitize(data.get("body"), 220)
                call = message.get("type") == 3 or bool(re.search(r"incoming call|is calling|входящий звонок", title + " " + body, re.I))
                channel_id = str(data.get("channel_id") or message.get("channel_id") or "")
                guild_id = str(message.get("guild_id") or data.get("guild_id") or "@me")
                route = (guild_id + "/" + channel_id
                         if re.fullmatch(r"@me|\d{17,22}", guild_id) and re.fullmatch(r"\d{17,22}", channel_id)
                         else "")
                if title:
                    send(f"DISCORD:{'call' if call else 'message'}|{title}|{body}|{route}")
            if time.monotonic() >= deadline:
                refresh()
                deadline = time.monotonic() + 8


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
    except (RuntimeError, EOFError, OSError, ValueError, queue.Empty) as error:
        sys.exit(f"Мост Discord: {error}")
