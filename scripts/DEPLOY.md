# FamLedger production deploy

## GitHub Actions → VPS

Secrets (repo → Settings → Secrets and variables → Actions):

| Secret | Value |
|--------|--------|
| `DEPLOY_HOST` | `159.195.114.77` |
| `DEPLOY_USER` | `root` |
| `DEPLOY_SSH_KEY` | private key from `/root/.ssh/github_actions_deploy` on the VPS |

Also add a **Deploy key** (read-only) on the repo: public key `/root/.ssh/famledger_git_deploy.pub` on the VPS.

On push to `master` (or manual `workflow_dispatch`), Actions SSHs in and runs `/root/deploy/bin/deploy-famledger.sh`.

## Backups

Container `famledger-pg-backup` dumps Postgres every 6 hours into `/root/backups/postgres/famledger` (keeps 14 days / 4 weeks / 3 months).

```bash
db-backup list
db-backup now
db-backup status
db-backup logs
```
