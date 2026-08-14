# Build / Test Plan — v0.3.0.6 FIX4

Run the normal Windows gate and passwordless Linux deployment.

Then rerun remote enrollment without any manual repair:

```bash
cd ~/mysttiq-builds/v0.3.0.6

bash ./scripts/Configure-MystTiqRemoteApi.sh \
  --bind 192.168.1.248
```

Required results:

- no `Text file busy`
- `service-install` succeeds while the previous MystTiq process is running
- systemd restarts successfully
- exact listener `192.168.1.248:8213` appears
- HTTPS health passes
- unauthenticated management request returns 401
- bearer-authenticated request returns 200
- final output reaches `ENROLLMENT PASS`

Then run the Windows remote API acceptance script and finally the loopback rollback test.
