namespace FileReadyCheckerConsole
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    public class FileReadyChecker
    {
        // 配置参数（可根据实际场景调整）
        private readonly int _maxWaitSeconds = 60; // 最大等待时间（秒）
        private readonly int _maxStableCount = 3; // 文件稳定次数（连续3次检测无变化则认为就绪）
        private readonly int _checkIntervalMs = 500; // 检测间隔（毫秒）
        private readonly int _fileAccessRetryCount = 3; // 单个文件访问重试次数
        private readonly int _fileAccessRetryDelayMs = 100; // 重试间隔（毫秒）

        private readonly string _recvFolder;
        private readonly ILogger<FileReadyChecker> _logger;

        public FileReadyChecker(string recvFolder, ILogger<FileReadyChecker> logger)
        {
            _recvFolder = recvFolder ?? throw new ArgumentNullException(nameof(recvFolder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 核心优化：检测文件是否已被释放（可独占打开），不依赖文件大小
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>true=文件已释放（就绪），false=仍在写入/锁定</returns>
        private async Task<bool> IsFileReleasedAsync(string filePath)
        {
            for (int i = 0; i < _fileAccessRetryCount; i++)
            {
                FileStream? stream = null;
                try
                {
                    // 关键：以独占方式打开文件（FileShare.None）
                    // 如果上位机还持有句柄，会抛出 IOException
                    stream = new FileStream(
                        filePath,
                        FileMode.Open,
                        FileAccess.ReadWrite, // 要求读写权限（确保完全独占）
                        FileShare.None,       // 不允许其他进程共享
                        bufferSize: 4096,
                        FileOptions.None
                    );

                    // 能走到这里，说明文件已被释放
                    return true;
                }
                catch (FileNotFoundException)
                {
                    // 文件不存在，直接返回未就绪
                    return false;
                }
                catch (IOException ex)
                {
                    // 常见异常：文件正被另一进程使用（上位机还在写入）
                    _logger.LogWarning("🔒 文件未释放（重试{RetryCount}/{MaxRetry}）：{FilePath}，异常：{Message}",
                        i + 1, _fileAccessRetryCount, filePath, ex.Message);
                    await Task.Delay(_fileAccessRetryDelayMs);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // 权限不足（可能上位机以管理员权限写入），降级为“大小检测”
                    _logger.LogWarning("🚫 无独占打开权限，降级为大小检测：{FilePath}，异常：{Message}",
                        filePath, ex.Message);
                    return await IsFileSizeStableAsync(filePath); // 降级方案
                }
                finally
                {
                    stream?.Dispose(); // 无论成功与否，都关闭流释放句柄
                }
            }

            // 多次重试后仍无法独占打开，认为未就绪
            return false;
        }

        /// <summary>
        /// 降级方案：检测文件大小是否稳定（应对权限不足场景）
        /// </summary>
        private async Task<bool> IsFileSizeStableAsync(string filePath)
        {
            try
            {
                // 连续读取2次大小，间隔100ms，确保大小不变
                long size1 = new FileInfo(filePath).Length;
                await Task.Delay(100);
                long size2 = new FileInfo(filePath).Length;
                return size1 == size2;
            }
            catch (Exception ex)
            {
                _logger.LogError("⚠️ 大小检测失败：{FilePath}，异常：{Message}", filePath, ex.Message);
                return false;
            }
        }

        // 配套修改：GetAccessibleCsvFilesAsync 改为获取“已释放”的文件
        private async Task<(List<string> ReadyFiles, Dictionary<string, string> FileStatuses)> GetReleasedCsvFilesAsync()
        {
            var readyFiles = new List<string>();
            var fileStatuses = new Dictionary<string, string>(); // 记录每个文件的状态（方便日志）

            try
            {
                var allCsvPaths = Directory.GetFiles(_recvFolder, "*.csv", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f)
                    .ToList();

                foreach (var filePath in allCsvPaths)
                {
                    bool isReleased = await IsFileReleasedAsync(filePath);
                    if (isReleased)
                    {
                        readyFiles.Add(filePath);
                        fileStatuses[filePath] = "✅ 已就绪";
                    }
                    else
                    {
                        fileStatuses[filePath] = "🔄 写入中/锁定";
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ 获取文件状态时发生异常");
            }

            return (readyFiles, fileStatuses);
        }

        // 主逻辑修改：判断文件就绪的核心改为“是否已释放”
        public async Task<List<string>> WaitForFilesReadyAsync()
        {
            int stableCount = 0;
            int totalRetry = 0;
            var maxTotalRetries = (int)(_maxWaitSeconds * 1000 / _checkIntervalMs);

            // 上次检测的“已就绪文件列表”（用于判断是否有新增）
            List<string> lastReadyFiles = new List<string>();

            while (totalRetry < maxTotalRetries)
            {
                totalRetry++;

                // 1. 获取所有已释放（就绪）的文件
                var (currentReadyFiles, fileStatuses) = await GetReleasedCsvFilesAsync();

                // 2. 日志输出详细状态（方便排查）
                LogFileStatuses(fileStatuses);

                // 3. 判断是否有新增的就绪文件
                bool hasNewReadyFile = currentReadyFiles.Except(lastReadyFiles).Any();

                if (hasNewReadyFile)
                {
                    // 有新增就绪文件，重置稳定计数
                    stableCount = 0;
                    lastReadyFiles = new List<string>(currentReadyFiles);
                    _logger.LogDebug("🔄 新增就绪文件，重置稳定计数");
                }
                else
                {
                    // 无新增，累计稳定次数
                    stableCount++;
                    _logger.LogDebug("🕒 连续无新增就绪文件 {StableCount}/{MaxStableCount} 次", stableCount, _maxStableCount);

                    // 满足稳定条件且有文件 -> 就绪
                    if (stableCount >= _maxStableCount && currentReadyFiles.Any())
                    {
                        _logger.LogInformation("✅ 所有CSV文件已生成完成（{FileCount}个），准备解析。", currentReadyFiles.Count);
                        Console.WriteLine($"✅ 所有CSV文件已生成完成（{currentReadyFiles.Count}个），准备解析。");
                        return currentReadyFiles;
                    }
                }

                await Task.Delay(_checkIntervalMs);
            }

            // 超时处理
            var timeoutMsg = $"⌛ 等待文件就绪超时（{_maxWaitSeconds}秒），当前已就绪{lastReadyFiles.Count}个文件。";
            Console.WriteLine(timeoutMsg);
            _logger.LogWarning(timeoutMsg);
            return lastReadyFiles;
        }

        /// <summary>
        /// 输出每个文件的详细状态
        /// </summary>
        private void LogFileStatuses(Dictionary<string, string> fileStatuses)
        {
            if (fileStatuses.Any())
            {
                var statusDetails = string.Join(" | ", fileStatuses.Select(kv => $"{Path.GetFileName(kv.Key)}: {kv.Value}"));
                Console.WriteLine($"📦 检测到 {fileStatuses.Count} 个CSV文件：{statusDetails}");
                _logger.LogDebug("📦 检测到 {Count} 个CSV文件：{StatusDetails}", fileStatuses.Count, statusDetails);
            }
            else
            {
                Console.WriteLine("⌛ 尚未检测到CSV文件...");
                _logger.LogDebug("⌛ 尚未检测到CSV文件...");
            }
        }
    }

    // 使用示例
    // var checker = new FileReadyChecker(recvFolder, _logger);
    // var readyFiles = await checker.WaitForFilesReadyAsync();
}
