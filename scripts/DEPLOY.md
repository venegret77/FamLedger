# FamLedger production deploy

The workflow builds the solution, runs .NET tests against PostgreSQL and Redis, and checks the web build and dependencies before opening SSH. Pull requests run verification only.

## VPS setup

Use the existing VPS account configured in DEPLOY_USER. The account needs Docker access and a clean Git checkout with access to the repository. The checkout defaults to FamLedger in the account's home directory. Set FAMLEDGER_APP_DIR in the remote SSH environment to use a different checkout.

The workflow fetches master and runs scripts/deploy-famledger.sh from the verified commit in that checkout.

Set these GitHub Actions secrets.

- DEPLOY_HOST and DEPLOY_USER identify the VPS and deploy account.
- DEPLOY_SSH_KEY is the SSH key for that account.

Each GitHub Actions run accepts the VPS host key on first connection and stores it in a temporary known_hosts file for that run.

In the VPS .env, set a randomly generated JWT_SECRET of at least 32 bytes and configure WEB_ORIGIN and WEB_PUBLIC_URL to the HTTPS origin. An existing strong secret can be retained. Changing the secret invalidates existing login cookies. Set unique PostgreSQL and MinIO credentials. The deploy script sets the ASP.NET Core and bot environments to Production.

The API accepts forwarded headers from loopback and from the optional TRUSTED_PROXY_IP. Set it only to the verified, stable address of the last proxy. Ensure the proxy overwrites untrusted client forwarding headers and keep the API port private. Without an explicit trusted proxy, the IP throttle behind nginx can group clients together.

Webhooks require an exact HTTPS URL from server configuration. Set App__AllowedWebhookUrls__0 and subsequent indexed values for individually approved destinations. Use destinations whose DNS and infrastructure you trust. Redirects are disabled. Leave the list empty to disable outbound webhooks.

Before deploying the period constraint, run the following query and review the operations in any duplicate periods before resolving them.

    SELECT "ContextId", count(*)
    FROM budget_periods
    WHERE NOT "IsClosed"
    GROUP BY "ContextId"
    HAVING count(*) > 1;

The deploy script checks API health, health through nginx, and the JavaScript content type. Automatic rollback requires all three previous application images. If Docker cannot retain one of them, the script logs a warning and continues the deploy. Image rollback does not roll back PostgreSQL migrations. Before a schema change, take a backup and decide whether restoring data is necessary if the migration is incompatible with the previous application.

Compose builds the MinIO community server and client from tagged upstream sources. The MinIO data volume is unchanged. Back up the volume before the first rollout of the source-built image.

## Backups and restore drills

The existing PostgreSQL backup job keeps local dumps. Schedule scripts/backup-famledger.sh separately to copy PostgreSQL and MinIO to an encrypted off-host restic repository. Configure a private absolute FAMLEDGER_BACKUP_DIR, RESTIC_REPOSITORY, and restic credentials in the scheduler. Rotate the private staging directories and configure restic retention according to available storage.

The PostgreSQL and MinIO copies are sequential. For a point-in-time pair, schedule a maintenance window and pause writes to both before exporting.

Practice recovery against isolated PostgreSQL and MinIO instances with different ports and credentials. Restore a chosen restic snapshot into a private staging directory. Use pg_restore --exit-on-error --no-owner to load postgres.dump into an empty database. Use mc mirror to import the minio directory into the isolated bucket. Start the API against those instances and verify migrations, a budget summary, transaction history, and avatar reads before relying on the backup.
