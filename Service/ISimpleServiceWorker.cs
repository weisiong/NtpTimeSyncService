/// @file ISimpleServiceWorker.cs
/// @author Ron Wilson

using System;

namespace NtpTimeSyncService
{
	interface ISimpleServiceWorker
	{
		void Init();
		void Run();
		void Cleanup();
	}
}
