# POtoCSV

指定したディレクトリ内にあるすべてのPOファイルの内容をマージして、1つのCSVファイルとして指定したファイルパスに出力するコンソールアプリケーション。  
引数は以下とする  

-input POファイル格納ディレクトリ  
-output CSVファイル出力ファイルパス  

例：POtoCSV.exe -input C:\Temp\foo -output C:\Temp\bar.csv  

どちらの引数も必須とする。  

inputで指定したディレクトリ以下には、以下のように言語別のディレクトリがあり、その中に拡張子poのファイルがあることを前提とする。

例：  
C:\Temp\foo\en\Game.po  
C:\Temp\foo\ja\Game.po  
C:\Temp\foo\zh-hans\Game.po  

ディレクトリ内にpoファイルがない場合は、そのディレクトリの処理は行わない（エラーとしない）。  

アプリケーションは、各poファイルを読み込んで、1つのCSVファイルとして出力する。  
指定したファイルパスにすでにファイルが存在している場合は上書きして出力する。
CSVのヘッダ行は、poファイルがある直近のディレクトリ名とする。  
各カラムはすべて""で囲み、改行はCR+LFとする。

例：  
"en","ja","zh-hans"
"Sun","太陽","太阳"
"Earth","地球","地球"
"Moon","月","月球"

dotnet publish -c Release -r win-x64 --self-contained false

./POtoCSV.exe -input C:\Temp\Localization\game -output C:\Temp\Localization\Game.csv

./POtoCSV.exe -po -output C:\Temp\Localization\Game2 -input C:\Temp\Localization\Game.csv