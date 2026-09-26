using AnimalsAutoML_ConsoleApp1;
using MLModel121_ConsoleApp1;
using System.Net.Http.Headers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

var recognitionService = new RekognitionService(
    "",
    ""
);

var bot = new TelegramBotClient(
    ""
);

var me = await bot.GetMe();

Console.WriteLine($"Бот запущений: @{me.Username}");

var userModes = new Dictionary<long, string>();
var userFirstPhotos = new Dictionary<long, byte[]>();

var mainKeyboard = new ReplyKeyboardMarkup(new[]
{
    new KeyboardButton[]
    {
        "🐾 Класифікація ML.NET"
    },

    new KeyboardButton[]
    {
        "🏷 Об'єкти",
        "📝 OCR Текст"
    },

    new KeyboardButton[]
    {
        "🛡 Модерація",
        "👤 Порівняти обличчя"
    }
})
{
    ResizeKeyboard = true
};

bot.OnMessage += NewMessageHandler;

async Task NewMessageHandler(Message message, UpdateType type)
{
    if (message.Text != null)
    {
        if (message.Text == "🐾 Класифікація ML.NET" ||
            message.Text == "🏷 Об'єкти" ||
            message.Text == "📝 OCR Текст" ||
            message.Text == "🛡 Модерація" ||
            message.Text == "👤 Порівняти обличчя")
        {
            userModes[message.Chat.Id] = message.Text;
            userFirstPhotos.Remove(message.Chat.Id);

            if (message.Text == "🐾 Класифікація ML.NET")
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    "🐾 Режим ML.NET вибрано!\n\n" +
                    "Надішліть фотографію або пряме посилання на зображення.",
                    replyMarkup: mainKeyboard
                );
            }
            else if (message.Text == "🏷 Об'єкти")
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    "🏷 Режим аналізу зображення вибрано!\n\n" +
                    "Надішліть фотографію.\n\n" +
                    "Бот визначить:\n" +
                    "• об'єкти;\n" +
                    "• confidence;\n" +
                    "• яскравість;\n" +
                    "• різкість;\n" +
                    "• контраст;\n" +
                    "• основні кольори.",
                    replyMarkup: mainKeyboard
                );
            }
            else if (message.Text == "📝 OCR Текст")
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    "📝 Режим OCR вибрано!\n\n" +
                    "Надішліть фотографію з текстом.",
                    replyMarkup: mainKeyboard
                );
            }
            else if (message.Text == "🛡 Модерація")
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    "🛡 Режим модерації вибрано!\n\n" +
                    "Надішліть фотографію для перевірки.",
                    replyMarkup: mainKeyboard
                );
            }
            else if (message.Text == "👤 Порівняти обличчя")
            {
                await bot.SendMessage(
                    message.Chat.Id,
                    "👤 Режим порівняння облич вибрано!\n\n" +
                    "Спочатку надішліть перше фото.",
                    replyMarkup: mainKeyboard
                );
            }

            return;
        }

        if (userModes.TryGetValue(message.Chat.Id, out var currentMode) &&
            currentMode == "🐾 Класифікація ML.NET" &&
            Uri.TryCreate(message.Text, UriKind.Absolute, out var imageUri))
        {
            await ProcessImageUrl(
                message.Chat.Id,
                imageUri
            );

            return;
        }

        await bot.SendMessage(
            message.Chat.Id,
            "Оберіть режим роботи за допомогою кнопок нижче:",
            replyMarkup: mainKeyboard
        );

        return;
    }

    if (message.Photo == null)
    {
        await bot.SendMessage(
            message.Chat.Id,
            "Будь ласка, надішліть фото.",
            replyMarkup: mainKeyboard
        );

        return;
    }

    if (!userModes.TryGetValue(
        message.Chat.Id,
        out var currentUserMode))
    {
        currentUserMode = "🐾 Класифікація ML.NET";
        userModes[message.Chat.Id] = currentUserMode;
    }

    var photo = message.Photo.Last();

    var file = await bot.GetFile(photo.FileId);

    var filePath = Path.Combine(
        Path.GetTempPath(),
        $"{photo.FileId}.jpg"
    );

    await using (var fileStream = new FileStream(
        filePath,
        FileMode.Create))
    {
        await bot.DownloadFile(
            file.FilePath!,
            fileStream
        );
    }

    var imageBytes = await File.ReadAllBytesAsync(filePath);

    try
    {
        if (imageBytes.Length < 1000)
        {
            await bot.SendMessage(
                message.Chat.Id,
                "⚠️ Зображення занадто маленьке або пошкоджене."
            );

            return;
        }

        if (currentUserMode == "🐾 Класифікація ML.NET")
        {
            await ProcessMLNetImage(
                message.Chat.Id,
                imageBytes
            );

            return;
        }

        await bot.SendMessage(
            message.Chat.Id,
            "🔄 Фото отримано. Починаю аналіз..."
        );

        if (currentUserMode == "🏷 Об'єкти")
        {
            await ProcessObjects(
                message.Chat.Id,
                imageBytes
            );
        }
        else if (currentUserMode == "📝 OCR Текст")
        {
            await ProcessOCR(
                message.Chat.Id,
                imageBytes
            );
        }
        else if (currentUserMode == "🛡 Модерація")
        {
            await ProcessModeration(
                message.Chat.Id,
                imageBytes
            );
        }
        else if (currentUserMode == "👤 Порівняти обличчя")
        {
            await ProcessFaceComparison(
                message.Chat.Id,
                imageBytes
            );
        }
    }
    catch (Exception ex)
    {
        await bot.SendMessage(
            message.Chat.Id,
            $"❌ Помилка обробки:\n{ex.Message}"
        );
    }
    finally
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}

async Task ProcessObjects(
    long chatId,
    byte[] imageBytes)
{
    var response =
        await recognitionService.AnalyzeAsync(imageBytes);

    var labels = response.Labels ?? new List<Amazon.Rekognition.Model.Label>();

    var messageText = "🏷 Результат аналізу зображення\n\n";

    if (labels.Count > 0)
    {
        messageText += "🔎 Знайдені об'єкти:\n";

        foreach (var label in labels)
        {
            messageText +=
                $"• {label.Name} — {label.Confidence:F1}%\n";
        }
    }
    else
    {
        messageText +=
            "🔎 Об'єктів із заданою впевненістю не знайдено.\n";
    }

    if (response.ImageProperties != null)
    {
        var quality =
            response.ImageProperties.Quality;

        if (quality != null)
        {
            messageText +=
                "\n📊 Характеристики фотографії:\n" +
                $"☀️ Яскравість: {quality.Brightness:F1}%\n" +
                $"🔍 Різкість: {quality.Sharpness:F1}%\n" +
                $"🌓 Контраст: {quality.Contrast:F1}%\n";
        }

        var colors =
            response.ImageProperties.DominantColors;

        if (colors != null && colors.Count > 0)
        {
            messageText += "\n🎨 Основні кольори:\n";

            foreach (var color in colors.Take(5))
            {
                var colorName =
                    !string.IsNullOrWhiteSpace(color.SimplifiedColor)
                        ? color.SimplifiedColor
                        : color.CSSColor;

                if (string.IsNullOrWhiteSpace(colorName))
                {
                    colorName = "Невідомий";
                }

                messageText +=
                    $"• {colorName} — {color.PixelPercent:F1}%";

                if (!string.IsNullOrWhiteSpace(color.HexCode))
                {
                    messageText +=
                        $" ({color.HexCode})";
                }

                messageText += "\n";
            }
        }
    }

    await bot.SendMessage(
        chatId,
        messageText
    );
}

async Task ProcessOCR(
    long chatId,
    byte[] imageBytes)
{
    var textDetections =
        await recognitionService.DetectTextAsync(imageBytes);

    var foundTexts = textDetections
        .Where(t => t.Type == "LINE")
        .Select(t => t.DetectedText)
        .ToList();

    if (foundTexts.Count > 0)
    {
        await bot.SendMessage(
            chatId,
            "📝 Знайдений текст:\n\n" +
            string.Join("\n", foundTexts)
        );
    }
    else
    {
        await bot.SendMessage(
            chatId,
            "📝 Текст на фотографії не знайдено."
        );
    }
}

async Task ProcessModeration(
    long chatId,
    byte[] imageBytes)
{
    var moderationLabels =
        await recognitionService.DetectModerationAsync(imageBytes);

    if (moderationLabels == null ||
        moderationLabels.Count == 0)
    {
        await bot.SendMessage(
            chatId,
            "🛡 Модерація завершена.\n\n" +
            "✅ Небезпечний або небажаний контент не знайдено."
        );

        return;
    }

    var messageText =
        "🛡 Результат модерації\n\n" +
        "⚠️ Виявлено категорії контенту:\n";

    foreach (var label in moderationLabels)
    {
        messageText +=
            $"• {label.Name} — {label.Confidence:F1}%";

        if (!string.IsNullOrWhiteSpace(label.ParentName))
        {
            messageText +=
                $" | Батьківська категорія: {label.ParentName}";
        }

        messageText += "\n";
    }

    await bot.SendMessage(
        chatId,
        messageText
    );
}

async Task ProcessFaceComparison(
    long chatId,
    byte[] imageBytes)
{
    if (!userFirstPhotos.ContainsKey(chatId))
    {
        userFirstPhotos[chatId] = imageBytes;

        await bot.SendMessage(
            chatId,
            "✅ Перше фото збережено!\n\n" +
            "Тепер надішліть друге фото для порівняння."
        );

        return;
    }

    var firstImageBytes =
        userFirstPhotos[chatId];

    userFirstPhotos.Remove(chatId);

    var response =
        await recognitionService.CompareFacesAsync(
            firstImageBytes,
            imageBytes
        );

    if (response.FaceMatches != null &&
        response.FaceMatches.Count > 0)
    {
        var bestMatch =
            response.FaceMatches
                .OrderByDescending(x => x.Similarity)
                .First();

        await bot.SendMessage(
            chatId,
            $"👤 Порівняння завершено!\n\n" +
            $"🎯 Схожість облич: {bestMatch.Similarity:F2}%"
        );
    }
    else
    {
        await bot.SendMessage(
            chatId,
            "👤 Обличчя не збігаються " +
            "або обличчя на фото не знайдено."
        );
    }
}

async Task ProcessMLNetImage(
    long chatId,
    byte[] imageBytes)
{
    await bot.SendMessage(
        chatId,
        "🔄 Зображення отримано. " +
        "ML.NET починає класифікацію..."
    );

    var input = new MLModel1.ModelInput
    {
        ImageSource = imageBytes
    };

    var result =
        MLModel1.Predict(input);

    var labels = MLModel1
        .GetSortedScoresWithLabels(result)
        .ToList();

    if (labels.Count == 0)
    {
        await bot.SendMessage(
            chatId,
            "❌ Модель не повернула результат."
        );

        return;
    }

    var bestResult = labels.First();

    var confidence =
        bestResult.Value * 100;

    await bot.SendMessage(
        chatId,
        $"🐾 Результат класифікації:\n\n" +
        $"🏷 Клас: {bestResult.Key}\n" +
        $"🎯 Впевненість: {confidence:F2}%"
    );
}

async Task ProcessImageUrl(
    long chatId,
    Uri imageUri)
{
    try
    {
        await bot.SendMessage(
            chatId,
            "🔗 Посилання отримано. " +
            "Завантажую зображення..."
        );

        using var httpClient =
            new HttpClient();

        httpClient.Timeout =
            TimeSpan.FromSeconds(20);

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "AnimalClassifierBot/1.0"
        );

        var response =
            await httpClient.GetAsync(imageUri);

        if (!response.IsSuccessStatusCode)
        {
            await bot.SendMessage(
                chatId,
                $"❌ Не вдалося завантажити зображення.\n" +
                $"Сервер повернув: " +
                $"{(int)response.StatusCode} " +
                $"({response.StatusCode})"
            );

            return;
        }

        var contentType =
            response.Content.Headers.ContentType?.MediaType;

        if (contentType == null ||
            !contentType.StartsWith("image/"))
        {
            await bot.SendMessage(
                chatId,
                "❌ Це не пряме посилання на зображення.\n\n" +
                "Потрібне посилання на файл .jpg, .jpeg, .png тощо."
            );

            return;
        }

        var imageBytes =
            await response.Content.ReadAsByteArrayAsync();

        if (imageBytes.Length < 1000)
        {
            await bot.SendMessage(
                chatId,
                "❌ Зображення занадто маленьке або пошкоджене."
            );

            return;
        }

        await ProcessMLNetImage(
            chatId,
            imageBytes
        );
    }
    catch (Exception ex)
    {
        await bot.SendMessage(
            chatId,
            $"❌ Помилка завантаження зображення:\n" +
            $"{ex.Message}"
        );
    }
}

Console.ReadLine();