mergeInto(LibraryManager.library, {
    OpenPlayerFilePicker: function (objectNamePtr) {
        var objectName = UTF8ToString(objectNamePtr);
        // Avoid overlapping dialogs and reads from repeated button clicks.
        if (window.lilaFilePickerBusy) return;
        window.lilaFilePickerBusy = true;

        var input = document.createElement("input");
        input.type = "file";
        input.multiple = false;
        // Unrestricted: source files use the .nakama-0 extension.
        input.style.display = "none";
        document.body.appendChild(input);

        function fail(message) {
            input.remove();
            window.lilaFilePickerBusy = false;
            SendMessage(objectName, "OnFilePickerError", message);
        }

        input.addEventListener("change", function () {
            var file = input.files[0];
            input.remove();
            if (!file) {
                fail("No file selected.");
                return;
            }

            var reader = new FileReader();
            reader.onload = function () {
                var result = reader.result;
                if (typeof result !== "string" || result.indexOf(",") < 0) {
                    fail("Could not read the selected file.");
                    return;
                }
                window.lilaFilePickerBusy = false;
                SendMessage(objectName, "OnParquetFileLoaded", JSON.stringify({
                    name: file.name,
                    base64: result.substring(result.indexOf(",") + 1)
                }));
            };
            reader.onerror = function () {
                fail("Could not read the selected file.");
            };
            reader.onabort = function () {
                fail("File reading was cancelled.");
            };
            try {
                reader.readAsDataURL(file);
            } catch (error) {
                fail("Could not read the selected file.");
            }
        });

        input.addEventListener("cancel", function () {
            fail("No file selected.");
        });

        try {
            input.click();
        } catch (error) {
            fail("Could not open the file picker.");
        }
    }
});
