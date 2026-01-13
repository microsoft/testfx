// Test device discovery
using Microsoft.Testing.Platform.Device;
using Microsoft.Testing.Platform.Device.Android;

Console.WriteLine("=== Android Device Discovery Test ===\n");

try
{
    var manager = new AndroidDeviceManager();
    Console.WriteLine("✅ AndroidDeviceManager created successfully");
    
    var filter = new DeviceFilter(Platform: PlatformType.Android);
    Console.WriteLine("📱 Discovering Android devices...\n");
    
    var devices = await manager.DiscoverDevicesAsync(filter, CancellationToken.None);
    
    Console.WriteLine($"Found {devices.Count} device(s):\n");
    
    foreach (var device in devices)
    {
        Console.WriteLine($"  Device ID:   {device.Id}");
        Console.WriteLine($"  Name:        {device.Name}");
        Console.WriteLine($"  Type:        {device.Type}");
        Console.WriteLine($"  Platform:    {device.Platform}");
        Console.WriteLine($"  State:       {device.State}");
        Console.WriteLine();
    }
    
    if (devices.Count > 0)
    {
        Console.WriteLine("✅ Device discovery successful!");
    }
    else
    {
        Console.WriteLine("⚠️ No devices found. Make sure:");
        Console.WriteLine("   1. ADB is installed and in PATH");
        Console.WriteLine("   2. An Android emulator is running, or");
        Console.WriteLine("   3. A physical device is connected via USB");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error: {ex.Message}");
    Console.WriteLine($"   {ex.GetType().Name}");
    
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
}
