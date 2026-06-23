# SaveFromSocialMediaTgBot

Telegram bot for downloading videos from supported social media links.

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
services:
  bot:
    image: shik8787/savefromsocialmediatgbot:arm64
    container_name: savefromsocialmedia-bot
    env_file:
      - .env
    environment:
      REDIS_CONNECTION_STRING: redis:6379,abortConnect=false
    depends_on:
      - redis
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    container_name: savefromsocialmedia-redis
    command: redis-server --appendonly yes
    volumes:
      - redis-data:/data
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
```

## Telegram group setup

If the bot should read links in groups or forum topics:

1. Open `@BotFather`.
2. Select your bot with `/mybots`.
3. Open `Bot Settings`.
4. Open `Group Privacy`.
5. Turn privacy mode off.
6. Add the bot to the group and give it permission to read messages.

Forum topics work as normal supergroup messages. The bot replies back to the same topic.
