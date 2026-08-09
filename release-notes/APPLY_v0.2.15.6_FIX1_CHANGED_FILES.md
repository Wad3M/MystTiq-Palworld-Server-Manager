# Apply Instructions — v0.2.15.6 FIX1 Changed Files

1. Start from the official v0.2.15.6 full source tree.
2. Stop MystTiq and PalServer before replacing application source/build output.
3. Extract the FIX1 changed-files archive over the repository root, preserving paths.
4. Unblock PowerShell scripts and run Clean, Validate, All.
5. Run the included regression harness from `scripts`.
6. Perform the targeted runtime-loaded tests in `BUILD_TEST_PLAN_v0.2.15.6_FIX1.md`.
7. Promote FIX1 into the v0.2.15.6 baseline only after compile and runtime validation.
