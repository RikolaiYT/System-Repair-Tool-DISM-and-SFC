# System Repair Tool (DISM, SFC & Diagnostics)

[![Download](https://img.shields.io/badge/Download-EXE-blue?style=for-the-badge\&logo=windows)](https://github.com/RikolaiYT/System-Repair-Tool-DISM-and-SFC/releases/download/1.0.1/System.Repair.Tool.exe)

---

## RU

Лёгкая утилита для диагностики, восстановления и базового обслуживания Windows.

Использует встроенные инструменты системы (DISM, SFC и др.), но делает это с нормальным UI, прогрессом и логированием.

---

### 🔧 Что делает программа

Основной сценарий (Repair):

* DISM /CheckHealth
* DISM /ScanHealth
* DISM /RestoreHealth
* SFC /scannow

Дополнительно (опционально):

* Очистка компонентов (WinSxS)
* Сброс сети (DNS + Winsock)
* Сбор информации о системе (systeminfo)

---

### ⚙️ Возможности

* Графический интерфейс (WPF, тёмная тема)
* Автоматический запуск с правами администратора
* Реальный прогресс (парсинг DISM и SFC)
* Отображение этапов выполнения
* Живой вывод без буферизации
* Фильтрация мусорного вывода DISM
* Корректные кодировки (OEM / Unicode)
* Обнаружение ошибок по выводу
* Автоматический запуск TrustedInstaller
* Сохранение лога в файл (UTF-8)
* Опциональные модули (например, сброс сети)

---

### ⚠️ Важно

* Сброс сети может повлиять на:

  * VPN
  * прокси
  * кастомные DNS

* Процесс может занимать 10–60 минут

* Не рекомендуется прерывать выполнение

* После завершения желательно перезагрузить систему

---

### ▶ Использование

1. Скачать `.exe` (кнопка выше)
2. Запустить
3. Подтвердить UAC
4. (Опционально) отключить ненужные модули
5. Нажать "Начать"
6. Дождаться завершения
7. Сохранить лог (по желанию)

---

## ENG

A lightweight utility for diagnosing and repairing Windows using built-in tools (DISM, SFC, etc.) with a proper UI and real-time feedback.

---

### 🔧 What it does

Core repair pipeline:

* DISM /CheckHealth
* DISM /ScanHealth
* DISM /RestoreHealth
* SFC /scannow

Optional modules:

* Component cleanup (WinSxS)
* Network reset (DNS + Winsock)
* System information report

---

### ⚙️ Features

* WPF GUI (dark theme)
* Automatic admin elevation
* Real progress tracking (DISM + SFC parsing)
* Step-by-step execution
* Live output (no buffering)
* DISM output filtering
* Proper encoding handling (OEM / Unicode)
* Basic error detection
* TrustedInstaller auto-start
* Log export (UTF-8)
* Optional modules (e.g., network reset)

---

### ⚠️ Notes

* Network reset may affect:

  * VPN configurations
  * proxy settings
  * custom DNS

* The process may take 10–60 minutes

* Do not interrupt execution

* Reboot is recommended after completion

---

### ▶ Usage

1. Download the `.exe`
2. Run the application
3. Accept UAC prompt
4. (Optional) disable modules
5. Click "Start"
6. Wait for completion
7. Save the log if needed

---

## Screenshot

![App Screenshot](screenshot.png)

---

## Requirements

* Windows 10 / 11
* .NET 6 / 7 / 8

---

## License

MIT License

---

## Author

Rikolai
https://github.com/RikolaiYT
