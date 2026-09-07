#!/usr/bin/env bash
# Jednorazowy skrypt do wyrobienia pierwszego certyfikatu Let's Encrypt.
# Uruchom z katalogu NoteEz-Server na serwerze, PO podmianie domeny w nginx/conf.d/noteez.conf.

set -e

DOMAIN="api.twoja-domena.pl"
EMAIL="twoj@email.pl"

if [ "$DOMAIN" = "api.twoja-domena.pl" ]; then
  echo "Najpierw podmien DOMAIN i EMAIL w tym skrypcie oraz w nginx/conf.d/noteez.conf"
  exit 1
fi

echo "1) Tworze tymczasowy samopodpisany certyfikat, zeby nginx w ogole wystartowal..."
docker compose -f docker-compose.prod.yml run --rm --entrypoint "\
  mkdir -p /etc/letsencrypt/live/$DOMAIN && openssl req -x509 -nodes -newkey rsa:2048 -days 1 \
  -keyout /etc/letsencrypt/live/$DOMAIN/privkey.pem \
  -out /etc/letsencrypt/live/$DOMAIN/fullchain.pem \
  -subj '/CN=localhost'" certbot

echo "2) Startuje nginx..."
docker compose -f docker-compose.prod.yml up -d nginx

echo "3) Kasuje tymczasowy certyfikat i wystepuje o prawdziwy..."
docker compose -f docker-compose.prod.yml run --rm --entrypoint "rm -rf /etc/letsencrypt/live/$DOMAIN /etc/letsencrypt/archive/$DOMAIN /etc/letsencrypt/renewal/$DOMAIN.conf" certbot

docker compose -f docker-compose.prod.yml run --rm certbot certonly \
  --webroot -w /var/www/certbot \
  -d "$DOMAIN" \
  --email "$EMAIL" --agree-tos --no-eff-email

echo "4) Restart nginx z gotowym certyfikatem..."
docker compose -f docker-compose.prod.yml restart nginx

echo "Gotowe. Certyfikat bedzie odnawiany automatycznie przez kontener certbot."
