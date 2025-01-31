# AutoTranslate 可以识别的 Font Asset 制作教程

如果你的字体不支持某些字符，需要导入自定义字体。EtG 中有两种字体，分别是 Daikon Forge 用的 dfFont 或 dfDynamicFont，用于按钮、标签，以及 tk2d 用的 tk2dFont ，用于对话框。

---

## **1. 下载 BMFont**

BMFont 是由 AngelCode 提供的免费工具，可以将 ttf 或 otf 字体文件转换为位图（png 文件）和字体描述文件（fnt 文件），用于 Unity 的 dfFont 或 tk2dFont。下载地址：[BMFont 官网](http://www.angelcode.com/products/bmfont/)

---

## **2. 用 BMFont 导出 fnt 文件**

### **步骤 1：选择和设置字体**
1. 打开 BMFont。
2. 点击菜单栏中的 **Options > Font Settings**。
3. 在弹出的窗口中：
   - **Font**: 选择你需要的字体（比如 TTF 文件）。也可以从 **Add font file** 导入。
   - **Size**: 设置字体大小，为确保清晰度，此处我选择76。
   - **Bold/Italic**: 可选，根据需要启用加粗或斜体效果。
   - 可以启用 **Render from TrueType outline**、**TrueType Hinting**、**Font Smoothing** 等渲染选项使字体更平滑。
   - **Charset **中选择 Unicode，包含中文或其他非英文字符。

<img src="https://imgbb.io/ib/qXuZNkM2s0sJjdp_1738226774.png" style="zoom:67%;" />

### **步骤 2：选择字符集**
1. 点击 OK 关闭 Font Settings，此时可以在右侧边栏选择需要的字符集。此处我把 Latin 和包含汉字的全部选上。
2. 若字体包含一些不需要的字符，可以菜单 **Edit > Select chars from file** 打开一个包含所有需要字符的 txt 文件，BMFont 会自动选择需要的字符。

<img src="https://imgbb.io/ib/0W5jNAVZRatvlsz_1738226954.png" style="zoom:67%;" />

### **步骤 3：设置输出选项**

1. 点击菜单栏中的 **Options > Save bitmap font as**，选择合适的路径。
2. 在窗口中进行选择：
   - **Padding**: 四个方向的字间距。
   - **Texture Width/Height**: 设置纹理的宽高（例如 256x256、512x512 或 1024x1024 等）此处字符较多，输入 4096x4096。
   - 勾选 **Bit Depth** 中的 32（支持透明背景）。
   - **Presets** 选择 White text with alpha。
   - **Font Descriptor**: 选择 Text 格式。
   - **Texture**: 选择输出格式 PNG。

<img src="https://imgbb.io/ib/cz9jqEemxg4hL5q_1738227048.png" style="zoom:67%;" />

### **步骤 4：生成位图和描述文件**
1. 配置完成后，点击菜单栏中的 **File > Save Bitmap Font As**。
2. 选择输出文件夹并保存保。存后会生成一个 png 文件（字体图像）和一个  fnt 文件（字体描述）。
   - **FNT 文件**: 是一个包含字符坐标、偏移量和其他信息的描述文件。
   - **PNG 文件**: 是一张包含所有字符的位图。

<img src="https://imgbb.io/ib/CcbP2naW9PNAciC_1738227119.png" style="zoom: 67%;" />

---

## **3. 在 Unity 生成字体**

### **步骤 1：配置 Unity 环境**

此处见 EtG Modding Guide 上的教程[Assetbundles: How-To](https://mtgmodders.gitbook.io/etg-modding-guide/misc/assetbundles-how-to)。请注意，此方法用到的工具仅供学习使用，**请勿用于其他用途**！

### **步骤 2：导入字体**

1. 将 ttf 或 tof 字体文件、上面生产的 fnt 文件和 png 文件拖入项目中。
2. 选择 png 文件，在右侧 Inspector 面板中，选择 **Texture Type** 为 Sprite (2D and UI)，勾选 **Advanced > Read/Write Enabled**。**Max Size **增大为创建的大小，此处为 4096。点击下方 **Apply**，若 png 较大需要一点时间的等待。

<img src="https://imgbb.io/ib/IjPaYm0pHf5yiSw_1738227198.png"  />

### **步骤 3：生成 dfFont 或 dfDynamicFont（推荐后者）**

1. （不推荐）生成 dfFont：
   1. ~~则右键 png 文件，**Daikon Forge > Texture Atlas > Create New Atlas**。<img src="https://imgbb.io/ib/rO1zZnHMdKx5ZC8_1738227479.png"  />~~
   2. ~~在生成的 atlas 的 Inspector 中将 **Max Size**不要小于图像尺寸，**Padding**设置为0。<img src="https://imgbb.io/ib/oSNSCl5LIreU18B_1738227337.png"  />~~
   3. ~~右键 fnt 文件，**Daikon Forge > Fonts > Create Bitmapped Font**。<img src="https://imgbb.io/ib/TnZggTaboetPSCk_1738227599.png"  />~~
   4. ~~在生成的 dfFont 的 Inspector 中将 **Atlas** 和 **Font Sprite** 设置为刚才生成的东西。<img src="https://imgbb.io/ib/jEQfcHyrKzXd1lx_1738227683.png"  />~~
2. （**推荐**）生成 dfDynamicFont：
   1. 则右键 ttf 或 otf 文件，**Daikon Forge > Font > Create Dynamic Font**。<img src="https://imgbb.io/ib/qI3iJ7YsKEohGMN_1738227761.png"  />
   2. 在生成的 dfDynamicFont 的 Inspector 中设置合适的字号。<img src="https://imgbb.io/ib/YrqOh6KZk0QwLmW_1738227803.png"  />

### **步骤 4：生成 tk2dFont**

1. 生成 tk2dFont。选中 fnt 文件和对应的 png 文件，右键，**Create > tk2d > Font**，修改生成的文件的名称。<img src="https://imgbb.io/ib/F5DXS61eWEySam2_1738227917.png"  />
2. 在右侧 Inspector 面板中选择 **Bm font** 为 fnt 文件，**Texture **为 png 文件。**Size Def **选择 Explicit，设置**Ortho Size**，如 5。点击下方 **Commit** 生成，会生成 tk2dFontData 的 prefab 和一个材质球。<img src="https://imgbb.io/ib/pvrbE6kPCTDsUB8_1738227962.png"  />
3. 在生成的 tk2dFontData 的 Inspector 中设置 **Texel Size** 为 **X 0.1**，**Y 0.02**，或你认为的合理值。<img src="https://imgbb.io/ib/eXZzi8xg1rkAeJB_1738228047.png"  />

---

## **4. 导出 AssetBundle**

1. 选中要导出的文件，在 Inspector 面板下方选择 **AssetBundle**，如果没有就新建。对所有要导出的文件，即刚才导入和生成的所有文件执行这一操作。<img src="https://imgbb.io/ib/42cP9ALqGBnBbqL_1738228151.png"  />
2. 在菜单 **AssetBundle > Build AssetBundle**，即可导出到 AssetBundles 目录。
3. 在同目录下的 manifest 文件中可以查看导出的 AssetBundle 和它的清单文件 manifest。manifest 文件中可以查看 AssetBundle 中的资源名称。<img src="https://imgbb.io/ib/8hPwZXSvjSeoDCl_1738228264.png" style="zoom:67%;" />

## **5. 修改 AutoTranslate 配置**

1. 将这个生成的 AssetBundle 放到 AutoTranslate 目录下。
2. 在 mod 管理器的配置界面的 **AutoTranslate > General** 选项卡设置 的 **Font asset bundle name **为 AssetBundle 的名称、**Custom df font name**为生成的 dfFont 或 dfDynamicFont 的名称、**Custom tk2d font name** 为生成的 tk2dFont 的名称。<img src="https://imgbb.io/ib/cXSFC899Z3plrOb_1738228349.png"  />

---
# AutoTranslate Font Asset Creation Tutorial

If your font doesn't support certain characters, you can import a custom font. EtG uses two types of fonts: the **dfFont** or **dfDynamicFont** used by Daikon Forge for buttons and labels, and the **tk2dFont** used for dialogue boxes.

---

## **1. Download BMFont**

BMFont is a free tool provided by AngelCode that converts TTF or OTF font files into bitmap (PNG) files and font description files (FNT), suitable for Unity's dfFont or tk2dFont. Download it here: [BMFont Official Website](http://www.angelcode.com/products/bmfont/)

---

## **2. Export FNT Files Using BMFont**

### **Step 1: Select Font**
1. Open BMFont.
2. Click **Options > Font Settings** in the menu bar.
3. In the window that pops up:
   - **Font**: Choose the desired font (e.g., TTF file). You can also import it using **Add font file**.
   - **Size**: Set the font size. To ensure clarity, set it to 76.
   - **Bold/Italic**: Optionally enable bold or italic styles.
   - You can enable options like **Render from TrueType outline**, **TrueType Hinting**, and **Font Smoothing** for smoother rendering.
   - In **Charset**, select **Unicode**, which includes Chinese characters or other non-English characters.

<img src="https://imgbb.io/ib/qXuZNkM2s0sJjdp_1738226774.png" style="zoom:67%;" />

### **Step 2: Select Character Set**
1. Click **OK** to close the Font Settings, then select the desired character set from the sidebar. I selected both Latin and Chinese characters.
2. If the font contains unnecessary characters, you can use **Edit > Select chars from file** to open a text file containing the characters you need. BMFont will automatically select the necessary characters.

<img src="https://imgbb.io/ib/0W5jNAVZRatvlsz_1738226954.png" style="zoom:67%;" />

### **Step 3: Set Output Options**
1. Click **Options > Save bitmap font as** and choose an appropriate output path.
2. In the window that appears:
   - **Padding**: Set the padding for all four directions.
   - **Texture Width/Height**: Set the texture size (e.g., 256x256, 512x512, or 1024x1024). For a large number of characters, set it to 4096x4096.
   - Check **Bit Depth** for 32 (supports transparent background).
   - **Presets**: Select **White text with alpha**.
   - **Font Descriptor**: Choose **Text** format.
   - **Texture**: Choose **PNG** output format.

<img src="https://imgbb.io/ib/cz9jqEemxg4hL5q_1738227048.png" style="zoom:67%;" />

### **Step 4: Generate Bitmap and Description Files**
1. Once all settings are configured, click **File > Save Bitmap Font As**.
2. Choose a folder and save the files. This will generate a PNG file (font image) and an FNT file (font description).
   - **FNT file**: Contains the character coordinates, offsets, and other information.
   - **PNG file**: A bitmap image containing all the characters.

<img src="https://imgbb.io/ib/CcbP2naW9PNAciC_1738227119.png" style="zoom: 67%;" />

---

## **3. Generate Fonts in Unity**

### **Step 1: Configure Unity Environment**
Follow the tutorial in the EtG Modding Guide [Assetbundles: How-To](https://mtgmodders.gitbook.io/etg-modding-guide/misc/assetbundles-how-to) for setting up the environment. Please note that the tools used in this method are for learning purposes only and **should not be used for other purposes**!

### **Step 2: Import Fonts**
1. Drag the TTF or OTF font file, the generated FNT file, and the PNG file into your Unity project.
2. Select the PNG file, and in the **Inspector** panel, set **Texture Type** to **Sprite (2D and UI)**. Check **Advanced > Read/Write Enabled**. Increase **Max Size** to match the image size (4096 in this case). Click **Apply**. It may take some time if the PNG is large.

<img src="https://imgbb.io/ib/IjPaYm0pHf5yiSw_1738227198.png"  />

### **Step 3: Generate dfFont or dfDynamicFont (Recommended: dfDynamicFont)**
1. (Not Recommended) Generate dfFont:
   1. ~~Right-click the PNG file and choose **Daikon Forge > Texture Atlas > Create New Atlas**.<img src="https://imgbb.io/ib/rO1zZnHMdKx5ZC8_1738227479.png"  />~~
   2. ~~In the generated atlas's **Inspector**, set **Max Size** to match the image size, and set **Padding** to 0.<img src="https://imgbb.io/ib/oSNSCl5LIreU18B_1738227337.png"  />~~
   3. ~~Right-click the FNT file and choose **Daikon Forge > Fonts > Create Bitmapped Font**.<img src="https://imgbb.io/ib/TnZggTaboetPSCk_1738227599.png"  />~~
   4. ~~In the generated dfFont's **Inspector**, set the **Atlas** and **Font Sprite** to the previously created assets.<img src="https://imgbb.io/ib/jEQfcHyrKzXd1lx_1738227683.png"  />~~
2. (Recommended) Generate dfDynamicFont:
   1. Right-click the TTF or OTF file and choose **Daikon Forge > Font > Create Dynamic Font**.<img src="https://imgbb.io/ib/qI3iJ7YsKEohGMN_1738227761.png"  />
   2. In the generated dfDynamicFont's **Inspector**, set the appropriate font size.<img src="https://imgbb.io/ib/YrqOh6KZk0QwLmW_1738227803.png"  />

### **Step 4: Generate tk2dFont**
1. Generate a tk2dFont by selecting the FNT file and the corresponding PNG file. Right-click and choose **Create > tk2d > Font**. Modify the name of the generated file.<img src="https://imgbb.io/ib/F5DXS61eWEySam2_1738227917.png"  />
2. In the **Inspector** panel, set **Bm font** to the FNT file and **Texture** to the PNG file. Set **Size Def** to **Explicit** and adjust the **Ortho Size**, e.g., 5. Click **Commit** to generate the tk2dFontData prefab and a material ball.<img src="https://imgbb.io/ib/YrqOh6KZk0QwLmW_1738227803.png"  />
3. In the generated tk2dFontData's **Inspector**, set the **Texel Size** to **X 0.1, Y 0.02**, or another reasonable value.<img src="https://imgbb.io/ib/eXZzi8xg1rkAeJB_1738228047.png"  />

---

## **4. Export AssetBundle**

1. Select the files to export and in the **Inspector** panel, select **AssetBundle**. If there isn't one, create a new AssetBundle. Apply this to all files you've imported and generated.<img src="https://imgbb.io/ib/42cP9ALqGBnBbqL_1738228151.png"  />
2. In the **AssetBundle** menu, choose **Build AssetBundle** to export the files to the AssetBundles directory.
3. In the same directory, the **manifest** file will show the AssetBundle and its asset list.<img src="https://imgbb.io/ib/8hPwZXSvjSeoDCl_1738228264.png" style="zoom:67%;" />

## **5. Modify AutoTranslate Configuration**

1. Place the generated AssetBundle in the AutoTranslate directory.
2. In the **AutoTranslate > General** tab of the mod manager's configuration interface, set **Font asset bundle name** to the AssetBundle name, **Custom df font name** to the generated dfFont or dfDynamicFont name, and **Custom tk2d font name** to the generated tk2dFont name.<img src="https://imgbb.io/ib/cXSFC899Z3plrOb_1738228349.png"  />