// 1. 配置 Serilog（核心步骤）
using FileReadyCheckerConsole;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

// --------------------------
// 1. 配置 Serilog 日志
// --------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "logs/test-file-checker-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7
    )
    .CreateLogger();

try
{
    // --------------------------
    // 2. 测试配置（可按需修改）
    // --------------------------
    string testFolder = @"\\192.168.0.209\sharedfolder-test"; // 你的共享文件夹
    int minFileCount = 20; // 最小生成文件数
    int maxFileCount = 60; // 最大生成文件数
    int simulateWriteDelayMs = 50; // 每个文件生成延迟（模拟真实写入）
    int loopCount = 10; // 循环测试次数（默认10次，可改20次）

    // --------------------------
    // 3. 初始化 DI 容器
    // --------------------------
    var services = new ServiceCollection()
        .AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddSerilog(Log.Logger);
        })
        .AddTransient<FileReadyChecker>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<FileReadyChecker>>();
            return new FileReadyChecker(testFolder, logger);
        });

    using var serviceProvider = services.BuildServiceProvider();
    var fileChecker = serviceProvider.GetRequiredService<FileReadyChecker>();
    var logger = Log.Logger;

    // 循环测试统计（记录成功/失败次数）
    int successCount = 0;
    int failCount = 0;
    var failDetails = new List<string>();

    logger.Information("======================================");
    logger.Information($"=== 开始循环测试 FileReadyChecker ===");
    logger.Information($"=== 测试次数：{loopCount} 次 ===");
    logger.Information($"=== 测试文件夹：{testFolder} ===");
    logger.Information($"=== 生成文件数：{minFileCount}~{maxFileCount} 个 ===");
    logger.Information("======================================");

    // --------------------------
    // 4. 循环执行测试
    // --------------------------
    for (int loop = 1; loop <= loopCount; loop++)
    {
        logger.Information($"\n📌 开始第 {loop}/{loopCount} 次测试");
        bool isCurrentTestSuccess = false;
        string currentFailReason = string.Empty;

        try
        {
            // 4.1 清理旧文件
            logger.Information("📥 清理旧 CSV 文件...");
            CleanOldFiles(testFolder);
            logger.Information("✅ 旧文件清理完成");

            // 4.2 模拟生成文件
            logger.Information("📤 模拟生成 CSV 文件...");
            var random = new Random();
            int actualFileCount = random.Next(minFileCount, maxFileCount + 1);
            var generatedFilePaths = new List<string>();

            // CSV 内容（复用你提供的内容）
            string csvContent = @"配方名,创建者,DES,RecipeItemName,English,Chinese,Value,Unit,MinValue,MaxValue
练靶_脉冲,1,,stRecipe.C1_ArSupplyTime,C1_ArSupplyTime,C1腔氩气供给时间,0,S,0,1000
练靶_脉冲,1,,stRecipe.C1_Chamb_Ar,C1_Chamb_Ar,C1腔氩气,0,sccm,0,1000
练靶_脉冲,1,,stRecipe.C7_Chamb_Ar,C7_Chamb_Ar,C7腔氩气,0,sccm,0,1000
练靶_脉冲,1,,stRecipe.CoatingSpeed,CoatingSpeed,镀膜速度,20,mm/s,0,100
练靶_脉冲,1,,stRecipe.CA1_Ar,CA1_Ar,CA1_氩气,350,sccm,0,2000
练靶_脉冲,1,,stRecipe.CA1_O2,CA1_O2,CA1_氧气,0,sccm,0,50
练靶_脉冲,1,,stRecipe.CA1_ArH2,CA1_ArH2,CA1_氩氢气,0,sccm,0,200
练靶_脉冲,1,,stRecipe.CA1_Power,CA1_Power,CA1_功率,0.5,KW,0,6
练靶_脉冲,1,,stRecipe.CA2_Ar,CA2_Ar,CA2_氩气,550,sccm,0,1000
练靶_脉冲,1,,stRecipe.CA2_O2,CA2_O2,CA2_氧气,0,sccm,0,50
练靶_脉冲,1,,stRecipe.CA2_ArH2,CA2_ArH2,CA2_氩氢气,0,sccm,0,200
练靶_脉冲,1,,stRecipe.CA2_Speed,CA2_Speed,CA2_速度,5,r/min,3,12
练靶_脉冲,1,,stRecipe.CA2_Power,CA2_Power,CA2_功率,0.9,KW,0,20
练靶_脉冲,1,,stRecipe.CA2_Frequency,CA2_Frequency,CA2_频率,40,KHZ,0,100
练靶_脉冲,1,,stRecipe.CA2_PulseTime,CA2_PulseTime,CA2_脉冲时间,20,us,3,495
练靶_脉冲,1,,stRecipe.CA3_Ar,CA3_Ar,CA3_氩气,850,sccm,0,1000
练靶_脉冲,1,,stRecipe.CA3_O2,CA3_O2,CA3_氧气,0,sccm,0,50
练靶_脉冲,1,,stRecipe.CA3_ArH2,CA3_ArH2,CA3_氩氢气,0,sccm,0,200
练靶_脉冲,1,,stRecipe.CA3_Speed,CA3_Speed,CA3_C_速度,5,r/min,3,12
练靶_脉冲,1,,stRecipe.CA3_Power,CA3_Power,CA3_C_功率,1,KW,0,20
练靶_脉冲,1,,stRecipe.CA4_Ar,CA4_Ar,CA4_氩气,500,sccm,0,1000
练靶_脉冲,1,,stRecipe.CA4_O2,CA4_O2,CA4_氧气,0,sccm,0,50
练靶_脉冲,1,,stRecipe.CA4_ArH2,CA4_ArH2,CA4_氩氢气,0,sccm,0,200
练靶_脉冲,1,,stRecipe.CA4_Speed,CA4_Speed,CA4_速度,5,r/min,3,12
练靶_脉冲,1,,stRecipe.CA4_Power,CA4_Power,CA4_功率,0.9,KW,0,20
练靶_脉冲,1,,stRecipe.CA4_Frequency,CA4_Frequency,CA4_频率,40,KHZ,0,100
练靶_脉冲,1,,stRecipe.CA4_PulseTime,CA4_PulseTime,CA4_脉冲时间,20,us,3,495
练靶_脉冲,1,,实验镀膜次数,Experiment Coating Numbers,实验镀膜次数,1,,1,50
练靶_脉冲,1,,实验镀膜折返腔体数值,Experiment Tray Turn Back Cavity Numbers,实验镀膜折返腔体数值,1,,1,4
练靶_脉冲,1,,stRecipe.C7_ArSupplyTime,C7_ArSupplyTime,C7腔氩气供给时间,0,S,0,1000";

            var generator = Task.Run(async () =>
            {
                // 使用 SemaphoreSlim 限制并发数为20
                var semaphore = new SemaphoreSlim(20, 20);

                // 并发生成文件
                var generateTasks = Enumerable.Range(1, actualFileCount)
                    .Select(async i =>
                    {
                        // 等待信号量，控制并发数
                        await semaphore.WaitAsync();
                        string fileName = $"配方文件_{Guid.NewGuid().ToString("N").Substring(0, 8)}.csv";
                        try
                        {
                            string filePath = Path.Combine(testFolder, fileName);
                            await Task.Delay(random.Next(10, simulateWriteDelayMs + 1));

                            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                            using (var writer = new StreamWriter(stream))
                            {
                                await writer.WriteAsync(csvContent);
                            }

                            logger.Debug($"✅ 生成文件：{fileName}");
                            generatedFilePaths.Add(filePath);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, $"❌ 文件生成失败：{fileName}");
                        }
                        finally
                        {
                            // 释放信号量
                            semaphore.Release();
                        }
                    });

                await Task.WhenAll(generateTasks);
                logger.Information($"✅ 生成完成：{actualFileCount} 个文件");
            });

            List<string>? readyFiles = new List<string>();

            var checker = Task.Run(async () =>
            {
                // 4.3 调用 FileReadyChecker 检测
                logger.Information("🔍 检测文件就绪状态...");
                var startTime = DateTimeOffset.Now;
                readyFiles = await fileChecker.WaitForFilesReadyAsync();
                var elapsedMs = (DateTimeOffset.Now - startTime).TotalMilliseconds.ToString("F0");

                // 4.4 验证结果
                logger.Information("📊 验证第 {loop} 次测试结果", loop);
                logger.Information($"检测耗时：{elapsedMs} ms");
                logger.Information($"生成文件数：{actualFileCount} | 检测就绪数：{readyFiles.Count}");
            });

            await Task.WhenAll(generator, checker);

            bool allFilesDetected = generatedFilePaths.All(readyFiles.Contains);
            if (allFilesDetected && readyFiles.Count == actualFileCount)
            {
                logger.Information("✅ 第 {loop} 次测试成功！", loop);
                isCurrentTestSuccess = true;
                successCount++;
            }
            else
            {
                var missingFiles = generatedFilePaths.Except(readyFiles).ToList();
                currentFailReason = $"文件数不匹配（生成{actualFileCount}个，检测{readyFiles.Count}个），未检测到：{string.Join(";", missingFiles.Select(Path.GetFileName))}";
                logger.Warning("⚠️ 第 {loop} 次测试失败：{Reason}", loop, currentFailReason);
                failCount++;
                failDetails.Add($"第 {loop} 次：{currentFailReason}");
            }
        }
        catch (Exception ex)
        {
            currentFailReason = $"异常：{ex.Message}";
            logger.Error(ex, "❌ 第 {loop} 次测试抛出异常：{Reason}", loop, currentFailReason);
            failCount++;
            failDetails.Add($"第 {loop} 次：{currentFailReason}");
        }

        logger.Information($"📌 第 {loop}/{loopCount} 次测试结束（{(isCurrentTestSuccess ? "成功" : "失败")}）");
    }

    // --------------------------
    // 5. 输出最终测试报告
    // --------------------------
    logger.Information("\n======================================");
    logger.Information($"=== 循环测试完成 ===");
    logger.Information($"总测试次数：{loopCount} 次");
    logger.Information($"成功次数：{successCount} 次");
    logger.Information($"失败次数：{failCount} 次");
    logger.Information($"成功率：{((double)successCount / loopCount * 100):F2}%");
    logger.Information("======================================");

    if (failDetails.Any())
    {
        logger.Warning("\n❌ 失败详情：");
        foreach (var detail in failDetails)
        {
            logger.Warning(detail);
        }
    }
    else
    {
        logger.Information("\n🎉 所有测试均成功！FileReadyChecker 稳定性验证通过！");
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ 循环测试程序执行失败");
}
finally
{
    await Log.CloseAndFlushAsync();
    Console.WriteLine("\n测试结束，按任意键退出...");
    Console.ReadKey();
}

// --------------------------
// 辅助方法：清理旧文件
// --------------------------
static void CleanOldFiles(string folderPath)
{
    if (!Directory.Exists(folderPath))
    {
        Directory.CreateDirectory(folderPath);
        return;
    }

    var oldCsvFiles = Directory.GetFiles(folderPath, "*.csv", SearchOption.TopDirectoryOnly);
    foreach (var file in oldCsvFiles)
    {
        try
        {
            File.Delete(file);
            Log.Debug($"🗑️ 删除旧文件：{Path.GetFileName(file)}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, $"⚠️ 无法删除旧文件：{Path.GetFileName(file)}");
        }
    }
}
