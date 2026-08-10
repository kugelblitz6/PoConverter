# PoConverter
## 概要
機能は以下の２つです。
- 指定したディレクトリ内にあるPOファイルからCSVファイルを出力する。
- 指定したCSVファイルからPOファイルを出力する。

CSVは1ファイルにまとめる方式と、`-split` オプションでmsgidの名前空間ごとに分割する方式のどちらでも扱えます。

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

エントリ数が増えて1つのCSVでは扱いにくくなったら、2と4に `-split` を付けてCSVを分割できます。

注：動作に問題がないか、まずテストプロジェクトで確認してください。

## 使い方（単一CSVファイル）
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

## 使い方（分割CSVファイル）

エントリ数が多いと、1つのCSVから目的の行を探すのが手間になります。  
`-split` を付けると、msgidの**名前空間ごと**にCSVを分割できます。

Unreal Engine5がCrowdin形式で出力するPOでは、msgidが `名前空間,キー` の形になります。  
この先頭のカンマまでの部分（ストリングテーブル名など）をファイル名に使います。

    "ST_Exhibition,Btn_Close"  →  Game_ST_Exhibition.csv
    "OrbitalElement,Apoapsis"  →  Game_OrbitalElement.csv

`-split` を指定しない場合の動作は単一CSVファイルのときと同じで、出力内容も変わりません。

### POファイルからCSVファイル出力（分割）

例：C:\Temp\fooディレクトリ内にあるPOファイルを、C:\Temp\Game_<名前空間>.csv として出力する。

    .\PoConverter.exe -mode csv -input C:\Temp\foo -output C:\Temp\Game.csv -split

各引数の意味は以下です。  
- -mode csv を指定することで、POファイルからCSVファイルへの出力が行われる。  
- -input 入力POファイルディレクトリ  
- -output **分割前の**出力CSVファイルパス  
- -split 名前空間ごとにCSVを分割する  

`-output` に指定したファイル自体は作られません。  
同じディレクトリに `Game_<名前空間>.csv` が名前空間の数だけ出力されます。

### CSVファイルからPOファイル出力（分割）

例：C:\Temp\Game_*.csvを、C:\Temp\fooディレクトリにpoファイルとして出力する

    .\PoConverter.exe -mode po -input C:\Temp\Game.csv -output C:\Temp\foo -split

各引数の意味は以下です。  
- -mode po を指定することで、CSVファイルからPOファイルへの出力が行われる。  
- -input **分割前の**入力CSVファイルパス  
- -output 出力POファイルディレクトリ  
- -split 分割されたCSVを読み込む  

`-input` に指定したファイル自体は読まれません。  
同じディレクトリの `Game_*.csv` をすべて読み込んで、1組のPOファイル（`<言語>\Game.po`）にまとめます。  
csv/poの両モードとも分割前のパスを指定する形にそろえてあるため、POファイル名は分割前の名前のまま決まります。

### 注意点

- **分割前のCSVは読み書きされません。** `-split` を付けたときに `Game.csv` が残っていると警告を表示します。古いファイルを編集してしまわないよう、分割へ移行したら削除してください
- **列構成（ヘッダー行）がファイル間で一致しないとエラーになります。** 言語列がずれたまま連結すると、翻訳が別の言語のPOに書き込まれてしまうためです
- **同じmsgidが複数のファイルに現れるとエラーになります。** 重複したmsgidを含むPOは不正で、どちらが採用されるか分からない状態でインポートさせないためです
- **POファイル内のエントリの並びが名前空間ごとにまとまります。** 分割前の並び順とは変わりますが、インポート時はmsgidで対応付けられるため結果は同じです
- 名前空間を持たないmsgid（カンマを含まないもの）は `_NoNamespace` にまとめられます

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

