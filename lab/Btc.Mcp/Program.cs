using Btc.Mcp;

if (args.Contains("--selftest")) return await SelfTest.Run();
await Transport.Serve();
return 0;
