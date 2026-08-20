# FamLedger

Семейный и личный бюджет: веб-интерface + Telegram-бот.

## Стек

- **Backend:** .NET 10, ASP.NET Core API, EF Core + PostgreSQL, Redis, MinIO
- **Frontend:** React 19, Vite, TypeScript, Tailwind CSS
- **Bot:** Telegram.Bot (long polling), workers (cron)

## Быстрый старт (одна команда)

```powershell
cd D:\PersonalProjects\FamLedger
.\start.ps1
```

Или двойной клик по `start.cmd`. Первый запуск с пересборкой: `.\start.ps1 -Rebuild`

Остановить: `.\start.ps1 -Down`

Linux/macOS: `./start.sh` / `./start.sh --rebuild`

Скрипт сам создаст `.env` из `.env.example`, если его нет. Укажите в `.env`:
- `TELEGRAM_BOT_TOKEN` — токен бота
- `TELEGRAM_BOT_USERNAME` — username бота (без @) для виджета входа

```bash
docker compose up -d --build
```

- Web: http://localhost:5173
- API: http://localhost:8080
- PostgreSQL: localhost:5435
- Redis: localhost:6382
- MinIO: http://localhost:9000 (console :9001)

## Локальная разработка

### Backend

```bash
docker compose up postgres redis minio -d
cd src/FamLedger.Api
dotnet run
```

### Bot

```bash
cd src/FamLedger.Bot
dotnet run
```

### Frontend

```bash
cd web/famledger-web
npm install
npm run dev
```

## Auth

Вход через [Telegram Login Widget](https://core.telegram.org/widgets/login). Укажите домен бота в @BotFather (`/setdomain`).

## Функции

- Личный и семейный бюджет, роли (Head / Assistant / Member)
- Плановые расходы, доходы, долги, копилка, цели
- Гибкий первый день отчётного месяца (default: 15)
- Мультивалютность RSD / EUR / USD
- Быстрая запись расходов через Telegram-бот
- Ежедневные пользовательские напоминания в Telegram (раздел «Напоминания»)
- Cron: rollover периодов, авто-списание, курсы, отправка напоминаний
