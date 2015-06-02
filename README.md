# Unity-waifu2x
This is a reimplementation of "waifu2x (converter only version)" for Unity ([original](https://github.com/WL-Amigo/waifu2x-converter-cpp)).
これはUnity用に "waifu2x (converter only version)" を再実装したものです。

Editor script that the scale processing by waifu2x in OnPostprocessTexture in AssetPostprocessor.
このエディタスクリプトはアセットポストプロセッサでインポートした画像を "waifu2x" を用いて拡大処理をします。



## System Requirements
- Windows DX11
- NVIDIA GPU
- Intel HD Graphics?
  
## How to use
Target image of more than 32px and square.
32px以上の正方形画像が対象です。
  
Please add options to the end of the file name.
ファイル名の後ろにオプションを追加してください。
  
- Scale only "XXXXXXXXXX**Waifu2x**.xxx"
- Scale and NoiseReduction type 1 "XXXXXXXXXX**NoiseReduction1Waifu2x**.xxx"
- Scale and NoiseReduction type 2 "XXXXXXXXXX**NoiseReduction2Waifu2x**.xxx"
