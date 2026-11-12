"""Forward new personal Telegram messages locally with an authorized MTProto session."""
import asyncio
import os
from pathlib import Path
import re
import sys

from island_ipc import send


def clean(value, size):
    return " ".join((value or "").replace("|", " ").split())[:size]


async def main():
    try:
        from telethon import TelegramClient, events, utils
    except ImportError:
        sys.exit("Установите Telethon в отдельное окружение Python.")
    api_id = os.environ.get("ISLAND_TELEGRAM_API_ID", "")
    api_hash = os.environ.get("ISLAND_TELEGRAM_API_HASH", "")
    if not api_id.isdecimal() or not re.fullmatch(r"[a-fA-F0-9]{32}", api_hash):
        sys.exit("Задайте свой API ID и API hash Telegram в переменных окружения.")
    profile = Path(os.environ["LOCALAPPDATA"]) / "WindowsDynamicIsland" / "TelegramBridge"
    profile.mkdir(parents=True, exist_ok=True)
    client = TelegramClient(str(profile / "telegram"), int(api_id), api_hash)
    await client.start()

    @client.on(events.NewMessage(incoming=True))
    async def forward(event):
        if not event.is_private:
            return
        person = await event.get_sender()
        if person is None:
            return
        name = clean(utils.get_display_name(person), 110) or "Telegram"
        body = clean(event.raw_text, 220) or "Вложение"
        username = getattr(person, "username", None) or ""
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_]{3,31}", username):
            username = ""
        await asyncio.to_thread(send, f"TELEGRAM:{username}|{name}|{body}")

    print("Мост Telegram подключён. Он показывает новые личные сообщения. Ctrl+C — выход.")
    await client.run_until_disconnected()


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
