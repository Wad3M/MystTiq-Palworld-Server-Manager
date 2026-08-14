# Build / Test Plan — v0.3.0.6 FIX3

Run the normal Windows and automated Linux gates, then rerun remote enrollment without manually repairing any files.

On Linux:

```bash
cd ~/mysttiq-builds/v0.3.0.6

bash ./scripts/Configure-MystTiqRemoteApi.sh \
  --bind 192.168.1.248
```

Required results:

- protected secrets directory reports owner `mystroth:mystroth`, mode `700`
- existing token is reused when present
- TLS PFX/password are created or reused automatically
- effective remote config readback succeeds
- exact `192.168.1.248:8213` listener appears
- HTTPS health passes
- unauthenticated management request returns 401
- authenticated management request returns 200
- final output is `ENROLLMENT PASS`

No manual token/certificate/config changes are allowed for acceptance.
