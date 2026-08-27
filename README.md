# SaveFromSocialMediaTgBot

Telegram bot for downloading media from supported social media links.

Instagram reels, single-photo posts, videos, and mixed photo/video carousels
are supported. Telegram albums are limited to the first 10 carousel items.

## Run on Raspberry Pi

The ARM64 image is already published to Docker Hub:

```bash
shik8787/savefromsocialmediatgbot:arm64
```

### 1. Install Docker

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
sudo reboot
```

After reboot, create a project folder:

```bash
mkdir -p ~/savefromsocialmedia-bot
cd ~/savefromsocialmedia-bot
```

### 2. Create `.env`

```bash
nano .env
```

Fill it with your bot credentials:

```env
TOKEN=your_telegram_bot_token
TWITTER_TOKEN=your_twitter_token
RETRY_COUNT=1
MAX_CONCURRENT_MEDIA=1
INST_LOGIN=
INST_PASSWORD=
INST_COOKIE_SESSION_ID=
Serilog__WriteTo__1__Args__hostnameOrAddress=
```

### 3. Create `docker-compose.yml`

```bash
nano docker-compose.yml
```

Paste:

```yaml
x-logging: &default-logging
  driver: json-file
  options:
    max-size: "10m"
    max-file: "3"

services:
  bot:
    image: shik8787/savefromsocialmediatgbot:arm64
    container_name: savefromsocialmedia-bot
    init: true
    env_file:
      - .env
    environment:
      REDIS_CONNECTION_STRING: redis:6379,abortConnect=false
    depends_on:
      redis:
        condition: service_healthy
    shm_size: "256m"
    tmpfs:
      - /tmp:size=256m,mode=1777
    pids_limit: 256
    logging: *default-logging
    restart: unless-stopped
    stop_grace_period: 30s

  redis:
    image: redis:7-alpine
    container_name: savefromsocialmedia-redis
    command: redis-server --appendonly yes --appendfsync everysec --auto-aof-rewrite-percentage 100 --auto-aof-rewrite-min-size 16mb
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 3s
      retries: 5
    logging: *default-logging
    restart: unless-stopped

volumes:
  redis-data:
```

### 4. Start

```bash
docker compose up -d
```

Check containers:

```bash
docker ps
```

Check bot logs:

```bash
docker logs -f savefromsocialmedia-bot
```

### 5. Update

```bash
cd ~/savefromsocialmedia-bot
docker compose pull
docker compose up -d
docker image prune -f
```

## Raspberry Pi maintenance

Use a reliable power supply and preferably run Docker data from a USB SSD
instead of a microSD card. Check storage and Docker growth regularly:

```bash
df -h
docker system df
docker logs --tail 100 savefromsocialmedia-bot
sudo du -sh /var/lib/docker
```

For Raspberry Pi models with 1 GB RAM, keep `MAX_CONCURRENT_MEDIA=1`. Larger
values allow parallel downloads but can cause Chromium memory pressure and
swap writes. Do not run `docker system prune --volumes`, because it can remove
the Redis volume containing chat settings.

## Telegram group setup

If the bot should read links in groups or forum topics:

1. Open `@BotFather`.
2. Select your bot with `/mybots`.
3. Open `Bot Settings`.
4. Open `Group Privacy`.
5. Turn privacy mode off.
6. Add the bot to the group and give it permission to read messages.

Forum topics work as normal supergroup messages. The bot replies back to the same topic.
