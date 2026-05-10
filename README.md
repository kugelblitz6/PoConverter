# PoConverter
## 概要
機能は以下の２つです。
- 指定したディレクトリ内にあるPOファイルからCSVファイルを出力する。
- 指定したCSVファイルからPOファイルを出力する。

POファイルが格納されているディレクトリには、言語別のディレクトリがあり、その中に拡張子poのファイルがあることを前提とします。

例：  
C:\Temp\foo\en\Game.po  
C:\Temp\foo\ja\Game.po  
C:\Temp\foo\zh-hans\Game.po  

## 想定運用方法
Unreal Engine5から出力されるpoファイルでの利用を想定しています。  
1. Unreal Engine5のローカリゼーションダッシュボードで、[テキストエクスポート]でPOファイルをエクスポートする
1. 本ツールで、POファイルからCSVファイルを生成する
1. CSVファイルを編集して、翻訳を行う
1. 本ツールで、CSVファイルからPOファイルを生成する
1. Unreal Engine5のローカリゼーションダッシュボードで、[テキストインポート]でPOファイルをインポートする

注：動作に問題がないか、まずテストプロジェクトで確認してください。

## 使い方
### POファイルからCSVファイル出力

例：C:\Temp\fooディレクトリ内にあるPOファイルを、bar.csvとして出力する。

    .\PoConverter.exe -mode csv -input C:\Temp\foo -output C:\Temp\bar.csv

各引数の意味は以下です。  
- -mode csv を指定することで、POファイルからCSVファイルへの出力が行われる。  
- -input 入力POファイルディレクトリ  
- -output 出力CSVファイルパス  

CSVファイルからPOファイルを出力するとき、CSVファイル名（拡張子なし）がPOファイル名となります。  
そのため、元のPOファイル名の拡張子を変えたもの（例：Game.po → Game.csv）をCSVファイル名として指定したほうが後の運用が楽になります。  

指定した出力先にすでに同名のCSVファイルが存在する場合、上書きされます。

### CSVファイルからPOファイル出力

例：C:\Temp\bar.csvを、C:\Temp\fooディレクトリにpoファイルとして出力する

    .\PoConverter.exe -mode po -input C:\Temp\bar.csv -output C:\Temp\foo

各引数の意味は以下です。  
- -mode po を指定することで、CSVファイルからPOファイルへの出力が行われる。  
- -input 入力CSVファイルパス  
- -output 出力POファイルディレクトリ  

## ビルド方法
.NET 10.0がインストールされていることを前提とします。
### .NETランタイムを同梱しない

実行端末にも.NET 10.0がインストールされている必要があります。

    dotnet publish -c Release -r win-x64 --self-contained false

### .NETランタイムを同梱する

実行端末に.NET10.0がインストールされている必要はありませんが、ランタイムを exe に同梱するため、ファイルサイズが大きくなります（100MB超）。

    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

## 免責事項
本ツールは無保証で提供されます。使用によって生じたいかなる損害についても、作者は責任を負いません。

