import time

PIPE = r"\\.\pipe\WindowsDynamicIsland_IPC_v1"


def send(message):
    payload = message.encode("utf-8")
    if len(payload) > 4096:
        raise ValueError("Island message exceeds 4 KiB")
    for attempt in range(3):
        try:
            with open(PIPE, "wb", buffering=0) as pipe:
                pipe.write(payload)
            return True
        except OSError:
            if attempt < 2:
                time.sleep(0.08)
    return False
