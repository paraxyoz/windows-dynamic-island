"""Track a Store/WinGet install or a 7-Zip extraction without inventing progress."""
import argparse
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid

from island_ipc import send

PERCENT = re.compile(r"(?<![\d.])(\d{1,3})(?:[.,]\d+)?\s*%")
SIZE = re.compile(r"([\d.,]+)\s*(B|KB|MB|GB|TB)\s*/\s*([\d.,]+)\s*(B|KB|MB|GB|TB)", re.I)
UNITS = {unit: 1024 ** i for i, unit in enumerate(("B", "KB", "MB", "GB", "TB"))}
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


def read_progress(line):
    line = ANSI.sub("", line)
    match = SIZE.search(line)
    if match:
        left, left_unit, right, right_unit = match.groups()
        try:
            current = float(left.replace(",", ".")) * UNITS[left_unit.upper()]
            total = float(right.replace(",", ".")) * UNITS[right_unit.upper()]
            return max(0, min(99, int(current * 100 / total))) if total > 0 else None
        except ValueError:
            return None
    match = PERCENT.search(line)
    return min(99, int(match[1])) if match and int(match[1]) <= 100 else None


def run(command, title):
    task_id = uuid.uuid4().hex
    if not send(f"TASK:{task_id}|pending|{title}"):
        print("Остров не запущен. Команда не была запущена.", file=sys.stderr)
        return 1
    process = None
    result = "failed"
    try:
        process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        previous = None
        line = bytearray()
        while True:
            char = process.stdout.read(1)
            if not char:
                break
            if char in (b"\r", b"\n"):
                text = line.decode("utf-8", errors="replace").strip()
                line.clear()
                if text:
                    print(ANSI.sub("", text), flush=True)
                    progress = read_progress(text)
                    if progress is not None and progress != previous:
                        previous = progress
                        send(f"TASK:{task_id}|{progress}|{title}")
            elif len(line) < 16384:
                line.extend(char)
        code = process.wait()
        result = "100" if code == 0 else "failed"
        return code
    except KeyboardInterrupt:
        result = "cancelled"
        if process and process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=4)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
        return 130
    finally:
        if process and process.stdout:
            process.stdout.close()
        send(f"TASK:{task_id}|{result}|{title}")


def main():
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    install = commands.add_parser("install")
    install.add_argument("package_id")
    install.add_argument("--source", default="msstore", choices=("msstore", "winget"))
    extract = commands.add_parser("extract")
    extract.add_argument("archive", type=Path)
    extract.add_argument("destination", type=Path)
    extract.add_argument("--seven-zip", type=Path)
    args = parser.parse_args()
    if args.command == "install":
        if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,100}", args.package_id):
            parser.error("Некорректный идентификатор пакета")
        winget = shutil.which("winget.exe")
        if not winget:
            parser.error("Требуется Windows App Installer (WinGet)")
        return run([winget, "install", "--id", args.package_id, "--exact", "--source", args.source],
                   f"Установка · {args.package_id}")

    archive = args.archive.resolve(strict=True)
    target = args.destination.resolve()
    if not archive.is_file() or archive == target:
        parser.error("Укажите файл архива и отдельную папку")
    fallback = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "7-Zip" / "7z.exe"
    seven_zip = args.seven_zip or Path(shutil.which("7z.exe") or fallback)
    if not seven_zip.is_file():
        parser.error("Установите 7-Zip или укажите путь --seven-zip")
    return run([str(seven_zip.resolve()), "x", "-bsp1", "-aos", "-o" + str(target), "--", str(archive)],
               ("Распаковка · " + archive.name)[:160])


if __name__ == "__main__":
    sys.exit(main())
