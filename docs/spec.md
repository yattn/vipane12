## Projcts説明
1. 目的
Windows 11上で、複数のフォルダを同時に表示・操作できる軽量ファイルマネージャ vipane12 を作る。
Windows Explorerの完全な代替は目指さない。
目的は、
複数の作業場所を常時並べ、その間でファイルを素早く操作すること
とする。

2. 設計原則
以下を最優先する。
KISS
YAGNI
Separation of Concerns
Least Mechanism
Explicit over Implicit
Windows標準機能を可能な限り利用する
外部依存を増やさない
SVN / Gitなど特定ツールへ依存しない
複雑な自動化より明示的な操作を優先する
状態を可能な限り持たない
独自フォーマットを可能な限り作らない
バグを増やす機能は実装しない
Vimの完全再現は行わない
ファイルマネージャ本体は、
表示・ファイル操作・外部コマンド起動
だけを担当する。
単一フォルダを大きく操作したい場合は、Windows標準Explorer等の既存ツールを使用する。

3. 動作環境
Name        vipane12
OS          Windows 11
Language    C#
UI          WinForms
Compiler    Windows環境の csc.exe

Windows専用とする。
クロスプラットフォーム対応は行わない。

4. ビルド制約
ビルドにはWindows 11環境で利用可能な csc.exe のみを使用する。
以下を要求しない。
Visual Studio
MSBuild
dotnet CLI
.NET SDK
NuGet
外部パッケージマネージャ
サードパーティDLL

ビルドは単純な build.bat から行えること。
build.bat
    ↓
csc.exe
    ↓
vipane12.exe


5. UI
標準状態では12個のファイルペインを同時表示する。
4列 × 3行

各ペインは独立したディレクトリを表示する。
UIは高密度かつ単純にする。
以下は持たない。
Ribbon
Tree View
Tab
Preview Pane
複雑なToolbar
ペイン最大化機能

12ペイン表示を本アプリの基本形とする。

6. FilePane
各ペインは以下のみを持つ。
FilePane
├── CurrentPath
├── PathInput
├── FileList
├── Selection
└── Refresh()

12ペインすべて同一実装を使用する。
ペイン固有の特殊処理は作らない。

7. パス表示・直接入力
各ペイン上部に現在パスを表示する。
パス欄は直接編集可能とする。
C:\work\project\src

ファイル一覧にフォーカスがある状態で、
e

を押すと、そのペインのパス入力欄へフォーカスを移動する。
パス入力欄では通常のWindows TextBoxとして文字入力できる。
Enter

で入力されたディレクトリへ移動する。
Esc

で変更を行わずファイル一覧へフォーカスを戻す。
存在しないパスやアクセス不能なパスの場合、
現在ディレクトリは変更しない
エラーを表示する
パス入力欄からファイル一覧へ戻る

8. キー入力の基本方針
Vim風キーバインドは、
ファイル一覧にフォーカスがある場合のみ有効
とする。
パス入力欄などのTextBoxにフォーカスがある場合は、通常のWindows文字入力として処理する。
独自のVimモード管理は実装しない。
存在する状態は実質的に、
FileListにFocus
PathInputにFocus

のみとする。
以下は実装しない。
Insert Mode
Visual Mode
Command-line Mode
Operator-pending Mode
Vim command parser
Key mapping engine


9. ファイル一覧内ナビゲーション
ファイル一覧にフォーカスがある場合、
j    次の項目
k    前の項目

とする。
一覧の先頭で k、末尾で j を押した場合は何もしない。
gg、G等の追加Vimコマンドはv0.1では実装しない。

10. ディレクトリ移動
親ディレクトリ
h
Backspace

のどちらでも現在ディレクトリの親へ移動する。
ドライブのルートでは何もしない。
フォルダを開く
l
Enter
Double Click

フォルダを選択している場合、そのペイン内で移動する。

11. ファイルを開く
l
Enter
Double Click

ファイルを選択している場合、Windowsの関連付けを使用して開く。
アプリ側でファイル形式ごとの処理は実装しない。

12. ペイン移動
キーボードでアクティブペインを変更できる。
Ctrl+h    左
Ctrl+j    下
Ctrl+k    上
Ctrl+l    右

4×3グリッド上の隣接ペインへ移動する。
移動先が存在しない場合は何もしない。
つまり、
h/j/k/l
    = ペイン内部

Ctrl+h/j/k/l
    = ペイン間

とする。

13. ファイル一覧
単一選択・複数選択に対応する。
表示順は固定とする。
Directory
    ↓
File

それぞれ名前の昇順で表示する。
列クリック等による複雑なソート機能は実装しない。

14. Hidden / Systemファイル
Hidden属性を持つファイル・ディレクトリも表示する。
System属性についても原則表示する。
つまり、
取得可能なファイルシステム項目は基本的にすべて表示する。
表示・非表示を切り替える設定は作らない。

15. Refresh
F5

でアクティブペインを再読み込みする。
リアルタイム監視は行わない。
FileSystemWatcher

は使用しない。
アプリ自身によるコピー・移動・削除後は、関連するペインをRefreshする。
外部アプリによる変更はF5で反映する。

16. コピー
以下のどちらでもコピーできる。
yy
Ctrl+C

選択項目をアプリ内Clipboardへ登録する。
複数選択にも対応する。
yy は、
y
↓
次のキーも y
↓
Copy

として処理する。
必要な状態は単純な一時フラグのみとし、汎用キーシーケンスエンジンは作らない。

17. 移動
以下のどちらでも移動待ち状態にできる。
dd
Ctrl+X

選択項目をアプリ内ClipboardへMoveとして登録する。
dd は、
d
↓
次のキーも d
↓
Cut

として処理する。
汎用Vim Operator機構は実装しない。

18. Paste
以下のどちらでも貼り付けできる。
p
Ctrl+V

貼り付け先は、
ActivePane.CurrentPath

とする。
ClipboardがCopy状態ならコピーする。
ClipboardがMove状態なら移動する。

19. アプリ内Clipboard
v0.1ではWindows Shell Clipboardへ依存しない。
アプリ内部に、
PendingOperation
├── Mode
│   ├── Copy
│   └── Move
└── Paths[]

を保持する。
そのため、
Explorer → vipane12
vipane12 → Explorer

間のCtrl+C / Ctrl+Vは対象外とする。

20. 上書き
コピー・移動先に同名項目が存在する場合、自動上書きしない。
複数項目操作を含め、選択肢は以下のみとする。
Replace All
Skip All
Cancel

Replace All
今回の操作中に発生する同名項目をすべて置換する。
Skip All
今回の操作中に発生する同名項目をすべてスキップする。
Cancel
残りの操作を中止する。
デフォルト選択は、
Skip All

とする。
安全側を標準とする。
以下は実装しない。
ファイルごとの個別確認
Apply to all
複雑な競合解決


21. 削除
ゴミ箱へ削除
以下のどちらでも実行できる。
x
Delete

選択項目をWindowsのゴミ箱へ移動する。
通常のゴミ箱削除では毎回確認しない。
完全削除
Shift+Delete

選択項目を完全削除する。
完全削除の場合のみ確認ダイアログを表示する。
Vim風の完全削除用キーは追加しない。

22. Vim風キーバインド一覧
Navigation
    j           次の項目
    k           前の項目
    h           親ディレクトリ
    l           開く

Pane
    Ctrl+h      左ペイン
    Ctrl+j      下ペイン
    Ctrl+k      上ペイン
    Ctrl+l      右ペイン

File
    yy          Copy
    dd          Cut / Move
    p           Paste
    x           Recycle Bin

Path
    e           パス欄を編集
    Esc         ファイル一覧へ戻る

Refresh
    F5          Refresh


23. Windows標準キーバインド
Vim風キーを追加しても、以下のWindows標準操作は残す。
Enter           Open
Double Click    Open

Backspace       Parent

Ctrl+C          Copy
Ctrl+X          Cut
Ctrl+V          Paste

Delete          Recycle Bin
Shift+Delete    Permanent Delete

F5              Refresh

Vim風操作とWindows標準操作は同じ内部処理を呼び出す。
yy
Ctrl+C
    ↓
CopySelection()

dd
Ctrl+X
    ↓
CutSelection()

p
Ctrl+V
    ↓
Paste()

x
Delete
    ↓
RecycleSelection()

操作ロジックを重複実装しない。

24. Vimキーシーケンス
v0.1で複数キーからなるVim風操作は、
yy
dd

のみとする。
必要な内部状態は、
PendingKey = None / Y / D

程度に限定する。
無関係な次キーが入力された場合はPending状態を解除する。
以下は実装しない。
gg
dw
dG
yy以外のyank operator
dd以外のdelete operator
count prefix
register
macro
repeat .
Vim grammar


25. エラー処理
最低限以下を扱う。
Access denied
File not found
Directory not found
File in use
Invalid path
Destination already exists
Copy failed
Move failed
Delete failed
External command failed

エラー発生時はアプリ全体を終了させない。
単純なエラーダイアログを表示する。
表示内容は、
Operation
Path
Error message

程度とする。
以下の高度な仕組みは実装しない。
Retry queue
Background retry
Recovery system
Transaction
Rollback
Error database


26. Workspace
12ペインの現在パスを保存・復元する。
複雑なWorkspace管理機能は持たない。
保存形式は、
workspace.txt

とする。
形式は、
1行 = 1ペイン
のみとする。
C:\work\src
C:\work\docs
C:\work\build
C:\work\release
C:\SVN\spec
C:\temp
C:\downloads
C:\shared
C:\Windows
C:\Program Files
C:\Users\user
D:\data

規則：
1行目    Pane 1
2行目    Pane 2
...
12行目   Pane 12

空行は既定ディレクトリとして扱う。
12行未満の場合、残りのペインは既定ディレクトリとする。
13行目以降は無視する。
文字コードはUTF-8とする。
JSON、INI、XML等は使用しない。

27. Workspace保存
アプリ終了時に現在の12ペインのパスを workspace.txt へ保存する。
起動時に読み込んで復元する。
保存済みパスが存在しない場合でもアプリ全体の起動は継続する。
該当ペインだけ既定ディレクトリへフォールバックする。

28. 設定保存場所
保存場所は portable.flag の存在だけで決定する。
Portable mode
vipane12.exe と同じディレクトリに、
portable.flag

が存在する場合、Portable modeとする。
vipane12\
├── vipane12.exe
├── portable.flag
├── workspace.txt
└── commands\

User mode
portable.flag が存在しない場合、User modeとする。
保存場所は、
%APPDATA%\vipane12\

とする。
%APPDATA%\vipane12\
├── workspace.txt
└── commands\

判定規則は以下のみ。
portable.flag あり
    → Portable mode

portable.flag なし
    → User mode

設定画面は作らない。

29. ネットワークパス
ネットワークファイルシステムは対象外とする。
\\server\share
UNC path
Network drive固有機能

ネットワーク関連の特殊処理は実装しない。

30. カスタムコマンド
特定ツール専用の統合機能は持たない。
代わりに、
選択項目と現在ディレクトリを外部コマンドへ渡す
汎用Mechanismを提供する。
vipane12本体は以下を知らない。
SVN
Git
TortoiseSVN
diff
editor
Explorer
その他の外部ツール


31. カスタムコマンドの保存形式
独自設定ファイルは作らない。
カスタムコマンドは、
commands\

ディレクトリ内の .cmd ファイルとして定義する。
commands\
├── SVN Update.cmd
├── SVN Commit.cmd
├── SVN Log.cmd
├── Diff.cmd
├── Open in Explorer.cmd
└── Command Prompt.cmd

1つの .cmd ファイル = 1つのカスタムコマンド
とする。

32. カスタムコマンド名
.cmd のファイル名をそのままUI上のコマンド名として使用する。
Open in Explorer.cmd

は、
Open in Explorer

として表示する。

33. カスタムコマンド引数
vipane12は .cmd 起動時に固定で以下を渡す。
%1 = 選択中の項目のフルパス
%2 = アクティブペインの現在ディレクトリ

Command.cmd "C:\work\src\main.c" "C:\work\src"

独自プレースホルダ機構は実装しない。

34. カスタムコマンド例
TortoiseSVN Update
@echo off
"C:\Program Files\TortoiseSVN\bin\TortoiseProc.exe" /command:update /path:"%~1"

Command Prompt
@echo off
cd /d "%~2"
cmd.exe

Windows Explorerで開く
@echo off
explorer.exe "%~2"

単一フォルダを高機能なUIで扱いたい場合はWindows Explorerへ委譲する。

35. カスタムコマンド列挙
起動時またはメニュー表示時に、
commands\*.cmd

を列挙する。
並び順はファイル名の昇順とする。
commands ディレクトリが存在しない場合は正常動作する。

36. カスタムコマンドUI
Open
Copy
Cut
Paste
Delete
----------------
Commands >
    SVN Update
    SVN Commit
    SVN Log
    Diff
    Open in Explorer
    Command Prompt

Windows Explorerの完全なShell Context Menuはホストしない。

37. カスタムコマンド実行責務
vipane12側の責務は、
.cmdを列挙
↓
選択された.cmdを起動
↓
%1 と %2 を渡す

までとする。
.cmd 内部の処理結果は原則として .cmd 側の責任とする。

38. 内部構造
Program.cs

MainForm.cs
    └── FilePane × 12

FilePane.cs
    ├── CurrentPath
    ├── Selection
    └── Refresh()

FileOperations.cs
    ├── Open()
    ├── Copy()
    ├── Move()
    └── Delete()

CommandRunner.cs
    ├── ListCommands()
    └── Run()

Workspace.cs
    ├── Load()
    └── Save()

build.bat


39. 責務
FilePane
表示
選択
j / k
h / l
パス入力
ディレクトリ移動
Refresh

FileOperations
Open
Copy
Move
Delete

CommandRunner
commands\*.cmd の列挙
.cmd の起動
%1 / %2 の引数受け渡し

Workspace
12個のパスの保存
12個のパスの復元


40. ファイル構成
Portable mode：
vipane12\
├── vipane12.exe
├── portable.flag
├── workspace.txt
└── commands\
    ├── SVN Update.cmd
    ├── SVN Log.cmd
    ├── Open in Explorer.cmd
    └── Command Prompt.cmd

User mode：
%APPDATA%\vipane12\
├── workspace.txt
└── commands\

ソースコード：
src\
├── Program.cs
├── MainForm.cs
├── FilePane.cs
├── FileOperations.cs
├── CommandRunner.cs
└── Workspace.cs

build.bat


41. v0.1 MUST
vipane12.exe

12ペイン
4×3固定レイアウト

パス直接入力

j / k
h / l
Ctrl+h/j/k/l

e
Esc

yy
dd
p
x

Enter
Backspace
Ctrl+C
Ctrl+X
Ctrl+V
Delete
Shift+Delete
F5

フォルダ移動
親ディレクトリ移動
ファイルを開く

単一選択
複数選択

コピー
移動
ゴミ箱削除
完全削除

Replace All
Skip All
Cancel

エラー処理

Hidden/System表示

workspace.txt
Workspace自動保存
Workspace自動復元

portable.flag
Portable mode
%APPDATA%\vipane12 User mode

commands\*.cmd
%1 = selected path
%2 = current directory

csc.exe直接ビルド
build.bat


42. MUST NOT
v0.1では以下を実装しない。
Windows Explorer完全互換

ペイン最大化
単一ペイン表示モード

Windows Shell Context Menu hosting

TortoiseSVN専用コード
SVN API
Git API
SVN状態取得
SVN overlay icon

独自Plugin API
Plugin loader

commands.txt
独自コマンド設定形式
独自テンプレートエンジン

JSON
INI parser
XML
データベース

FileSystemWatcher
リアルタイム監視

非同期Job Manager
コピーキュー
バックグラウンド転送

Drag & Drop
Windows Clipboard統合

タブ
ツリービュー
プレビュー
検索エンジン

ネットワークファイルシステム対応

設定画面
テーマシステム
レイアウトエディタ

個別ファイルごとの上書き確認
Apply to all UI

Vim完全互換
Vim Mode system
Vim command parser
Command-line Mode
Visual Mode
Register
Macro
Count prefix
汎用Key Mapping
Command Palette

gg
G
dw
dG
.
その他の高度なVim操作

Visual Studio依存
MSBuild依存
dotnet CLI依存
.NET SDK依存
NuGet
外部ライブラリ

DI framework
MVVM framework


43. 操作体系
                12 Pane
                    │
          ┌─────────┴─────────┐
          │                   │
      Pane内操作          Pane間操作
          │                   │
       h j k l           Ctrl+h j k l

ファイル操作は、
Vim風           Windows標準

yy       ─┐
Ctrl+C   ─┴─ Copy

dd       ─┐
Ctrl+X   ─┴─ Move

p        ─┐
Ctrl+V   ─┴─ Paste

x        ─┐
Delete   ─┴─ Recycle Bin

のように、入力方法だけを複数提供する。
内部処理は共通化する。

44. 最終設計思想
vipane12 の価値は、
12個の場所を同時に見ながら、Vim風の少ないキー操作でその間を移動・操作できること
に限定する。
FilePane
    = 見る・移動する

FileOperations
    = ファイルを操作する

CommandRunner
    = 外部コマンドを起動する

Workspace
    = 作業場所を覚える

Vimそのものは作らない。
j/k
    = 一覧移動

h/l
    = 階層移動

Ctrl+h/j/k/l
    = ペイン移動

yy/dd/p/x
    = 最小限のファイル操作

だけを借りる。
外部機能は再実装しない。
選択
  ↓
CommandRunner
  ↓
*.cmd
  ↓
外部ツール

設定についても、
portable.flag
workspace.txt
commands\*.cmd

だけで表現する。
本体が知る必要があるのは、
どこを表示しているか
 何を選択しているか
 何をどこへ操作するか
 どの .cmd を起動するか
だけとする。

追加要件
本書は vipane12 既存要件に対する追加仕様とする。
追加対象は以下の6点。
1. カスタムコマンドのVim風選択
2. Hierarchical Fuzzy Jump
3. Unicode / マルチバイト文字対応
4. v による連続範囲選択
5. Space による非連続選択
6. x による削除時の確認

既存のKISS / YAGNI / Least Mechanism方針を維持する。

1. カスタムコマンドのVim風選択
ファイル一覧にフォーカスがある状態で、
c

を押すと、
commands\*.cmd

を一覧表示する。
例：
Commands

> Command Prompt
  Diff
  Open in Explorer
  SVN Commit
  SVN Log
  SVN Update

操作：
j       次
k       前

Enter   実行
l       実行

Esc     キャンセル

選択された .cmd は既存の CommandRunner から起動する。
%1 = 選択中の項目のフルパス
%2 = アクティブペインの現在ディレクトリ

Vim Command-line ModeやCommand Paletteは作らない。

2. SelectionList
カスタムコマンド選択およびFuzzy Jump候補選択では、共通の単純な選択UIを使用する。
SelectionList
├── Items
├── SelectedIndex
└── Result

操作：
j       Down
k       Up

Enter   Select
l       Select

Esc     Cancel

用途ごとに別の選択UIは作らない。

3. Hierarchical Fuzzy Jump
3.1 目的
深く構造化されたディレクトリ階層に対して、途中の階層を省略しながら目的地へ移動できるようにする。
一般的なファイル検索機能は実装しない。

4. 入力方式
パス入力欄は、
e

で編集する。
通常Windowsパス
C:\work\project

は通常パスとして扱う。
完全一致のみとする。
Windowsパス自体のFuzzy補正は行わない。
Fuzzy Jump
10foobar

または、
10fo/20ba/30aa

の形式を使用する。
/ をFuzzy Jump専用の区切りとする。

5. Fuzzy Jumpの検索root
検索開始地点は常に、
ActivePane.CurrentPath

とする。
PC全体やドライブ全体は検索しない。

6. 単一Fuzzy Jump
例えば、
CurrentPath:
C:\SVN

実構造：
C:\SVN
└─ ProjectA
   └─ Design
      └─ 10foobar

入力：
10fo

なら、
C:\SVN\ProjectA\Design\10foobar

へ移動できる。

7. Hierarchical Fuzzy Jump
入力：
10fo/20ba/30aa

の場合、
root = CurrentPath

10fo
 ↓
root配下から再帰探索
 ↓
候補決定
 ↓
新root

20ba
 ↓
新root配下から再帰探索
 ↓
候補決定
 ↓
新root

30aa
 ↓
候補決定

の順に処理する。
途中階層は省略可能とする。

8. 候補判定
優先順位：
1. 完全一致
2. 大文字小文字を無視した完全一致
3. Prefix一致
4. Fuzzy一致

上位の一致方式で候補が見つかった場合、下位方式は使用しない。

9. Fuzzy距離
Fuzzy一致には、
Damerau-Levenshtein Distance

または同等の単純な文字列距離を使用する。
距離制限：
文字数 1～3
    distance <= 1

文字数 4以上
    distance <= 2

固定値とする。
設定化しない。

10. 候補決定
候補が1件なら自動決定する。
複数なら SelectionList を表示する。
Select directory

> 10foobar
  10foobar_old
  10foo_test

操作：
j
k
Enter / l
Esc

全候補経路の組み合わせ探索はしない。
1トークンずつ決定する。

11. Fuzzy Jump失敗
どこか1トークンでも解決できなければ、Fuzzy Jump全体を失敗とする。
途中まで成功していてもCurrentPathを変更しない。
Resolve
 ↓
Resolve
 ↓
Resolve
 ↓
全部成功した場合のみ
 ↓
Navigate

原子的に扱う。

12. 探索対象
対象は、
Directory

のみ。
ファイル検索は行わない。
アクセス不能ディレクトリはスキップし、探索可能な範囲を継続する。

13. 検索方式
v0.1では、
検索インデックスなし
検索DBなし
検索キャッシュなし
履歴なし
学習なし

とする。
必要になるまで高速化しない。

14. Unicode / マルチバイト文字対応
Windows上のUnicodeパスを扱えること。
対象例：
日本語
全角英数字
半角カナ
結合文字
BMP外文字
サロゲートペア

例：
C:\仕事\仕様書\１０_設計
C:\開発\ドライバ
C:\資料\𠮷田


15. Unicode正規化
Fuzzy Jumpの候補比較用文字列のみ、
NormalizationForm.FormKC

で正規化する。
つまり、
Original:
１０_仕様

Compare:
10_仕様

とする。
実ディレクトリ名や実パスは変更しない。

16. Text Element単位の比較
Fuzzy距離計算は可能な限りUTF-16 char 単位ではなく、
System.Globalization.StringInfo

等を用いたText Element単位で行う。
概念：
Original String
 ↓
Normalize(FormKC)
 ↓
Text Elementへ分割
 ↓
Damerau-Levenshtein

外部Unicodeライブラリは使用しない。

17. WorkspaceのUnicode
workspace.txt はUTF-8とする。
日本語等を含むパスをそのまま保存・復元できること。

18. カスタムコマンドへのUnicode受け渡し
.cmd の、
%1
%2

には比較用正規化文字列ではなく、Originalの実パスを渡す。
外部ツール自身のUnicode対応はvipane12の責務外とする。

明示的な選択がある場合、%1 には表示順で先頭の選択項目を渡す。
選択がない場合はカーソル上の項目、空の一覧では空文字列を渡す。

19. 選択モデル
ファイル・ディレクトリの選択は、最終的にすべて、
SelectedItems[]

として扱う。
選択機構は項目の種類を知らない。
つまり、
File
Directory
Hidden
System

を同一の選択対象として扱う。
ファイルかディレクトリかの判断は FileOperations 側で行う。

20. Space による非連続選択
20.1 目的
連続していない複数項目をVim風の簡単な操作で選択できるようにする。
Space

を現在項目の選択トグルとする。

21. Space の動作
現在項目が未選択の場合：
Space
 ↓
選択

現在項目が選択済みの場合：
Space
 ↓
選択解除

つまり、
Space = ToggleSelection(CurrentItem)

とする。

22. 非連続選択例
表示：
> file1
  file2
  file3
  file4
  file5

操作：
Space
jj
Space
jj
Space

結果：
* file1
  file2
* file3
  file4
* file5

のような非連続選択を可能とする。

23. Space とカーソル
Space は現在項目の選択状態のみ変更する。
カーソル位置は変更しない。
次の項目へ自動移動する処理は行わない。
移動は明示的に、
j
k

で行う。

24. v による連続範囲選択
24.1 目的
v

で現在位置を起点とした連続範囲を選択できるようにする。
v は完全なVim Visual Modeではなく、
連続範囲選択
のみを提供する。

25. v 開始
ファイル一覧にフォーカスがある状態で、
v

を押す。
その時点の選択状態を、
BaseSelection

として保存する。
現在位置を、
SelectionAnchor

として保持する。
既存Selectionは削除しない。

26. v 選択中の j / k
v 選択中は、
j
k

でカーソルを移動する。
現在の選択状態は、
BaseSelection
+
Range(SelectionAnchor, CurrentIndex)

とする。
範囲は常に、
min(anchor, current)
～
max(anchor, current)

で求める。
上下方向で別ロジックを作らない。

27. 既存選択と範囲選択の組み合わせ
例えば、
* file2

がSpaceによって既に選択されている状態で、
file5 から、
v
jj
v

と操作した場合、
* file2

* file5
* file6
* file7

とする。
つまり、
既存の非連続選択を保持したまま、連続範囲を追加できる。

28. v の確定
範囲選択中に再度、
v

を押すと、
RangeSelecting = false

とする。
現在の選択状態は維持する。

29. v のキャンセル
範囲選択中に、
Esc

を押した場合、
範囲選択開始前の、
BaseSelection

へ戻す。
その後、
RangeSelecting = false

とする。
つまりEscは、
今回のv操作だけをキャンセルする。
既存のSpace等による選択まで消さない。

30. 複数範囲選択
以下のような操作を許可する。
v
jj
v

jjjj

v
jj
v

結果として、離れた複数の連続範囲を選択できる。
専用の複数範囲管理機構は作らない。
既存Selectionへ範囲を追加するだけとする。

31. フォルダ・ファイル混在選択
Space および v は、項目の種類を区別しない。
例えば、
* 📁 src
* 📁 docs
* 📄 memo.txt
* 📄 spec.xlsx

のような混合選択を許可する。
選択側では、
Directory.Exists()
File.Exists()

等の判定を行わない。

32. 選択後の操作
選択されたすべての項目に対して、既存の操作をそのまま適用する。

明示的な選択がない場合は、カーソル上の1項目を操作対象とする。
Spaceによる選択解除やvのキャンセル後も同じ規則を適用する。
空の一覧ではファイル操作を行わず、Copy / Cutでは古いClipboard登録を解除する。
操作対象を決めるだけで、選択表示や新しいModeは追加しない。
yy      Copy
dd      Cut / Move
x       Recycle Bin

Windows標準キー：
Ctrl+C
Ctrl+X
Delete
Shift+Delete

も同様とする。

33. FileOperationsへの受け渡し
選択後は、
SelectedPaths[]

だけを FileOperations へ渡す。
例：
SelectedPaths
├── C:\work\src\
├── C:\work\memo.txt
├── C:\work\docs\
└── C:\work\spec.xlsx

FileOperations側で各項目について、
Directory
File

を判断して処理する。
選択機構とファイル操作機構を分離する。

34. コピー・移動・削除後
以下を実行した場合、
yy
dd
x
Ctrl+C
Ctrl+X
Delete
Shift+Delete

現在のRangeSelection状態は終了する。
ただし、
yy
Ctrl+C

のようなClipboard登録だけの場合は、選択表示自体を維持してよい。
実ファイル操作が完了して項目一覧がRefreshされた場合は、Selectionを再構築しない。
Refresh後の選択は解除してよい。
以前の行番号を使って選択を復元してはならない。別の項目が操作対象になるためである。

35. x による削除
x は誤操作防止のため、必ず確認ダイアログを表示する。
対象はWindowsのゴミ箱への削除。
例：
Move 3 selected items to Recycle Bin?

Yes
No

デフォルトは、
No

とする。

36. x と Delete の違い
Vim風キー：
x

では必ず確認する。
Windows標準キー：
Delete

では既存仕様どおり、確認せずゴミ箱へ移動する。
x
 ↓
Confirmation
 ↓
Recycle Bin

Delete
 ↓
Recycle Bin

とする。

37. Shift+Delete
Shift+Delete

は完全削除とする。
必ず確認する。
デフォルトはキャンセル側とする。

38. 削除ロジックの共通化
x と Delete で削除処理自体を重複実装しない。
x
 ↓
Confirm
 ↓
RecycleSelection()

Delete
 ↓
RecycleSelection()

とする。
確認の有無だけ入力側で変える。

39. 選択状態
選択に必要な内部状態は以下程度に限定する。
SelectedItems

RangeSelecting : bool
SelectionAnchor : int
BaseSelection

Space用に特別なModeは作らない。
Space
 ↓
SelectedItems.Toggle()

のみとする。

40. Vim風キーバインド
Navigation
    j           次
    k           前
    h           親
    l           開く

Pane
    Ctrl+h      左
    Ctrl+j      下
    Ctrl+k      上
    Ctrl+l      右

Selection
    Space       現在項目の選択トグル

    v           範囲選択開始
    j / k       範囲変更
    v           範囲確定
    Esc         今回の範囲選択をキャンセル

File
    yy          Copy
    dd          Cut / Move
    p           Paste
    x           確認後 Recycle Bin

Path
    e           PathInput

Command
    c           Commands一覧

Refresh
    F5          Refresh


41. 選択操作の役割
操作体系は以下のように分離する。
j / k
    = カーソル移動

Space
    = 単一項目の追加 / 解除

v
    = 連続範囲の追加

yy / dd / x
    = 選択項目への操作

つまり、
単品選択      Space
連続選択      v
非連続選択    Space / 複数回のv
操作          yy / dd / x

とする。

42. 内部構造
追加後も基本構造を維持する。
Program.cs

MainForm.cs
    └── FilePane × 12

FilePane.cs
    ├── CurrentPath
    ├── SelectedItems
    ├── RangeSelecting
    ├── SelectionAnchor
    ├── BaseSelection
    └── Refresh()

FileOperations.cs

CommandRunner.cs

Workspace.cs

FuzzyJump.cs

SelectionList.cs

build.bat

VisualMode.cs、SelectionEngine.cs 等の独立した複雑な機構は原則作らない。

43. FilePaneの選択責務
FilePaneは、
j / k カーソル移動

Space 選択トグル

v 範囲選択開始
j / k 範囲更新
v 範囲確定
Esc 範囲キャンセル

を担当する。
File / Directoryの種類判定は担当しない。
Copy / Move / Deleteも担当しない。

44. FileOperationsの責務
FileOperationsは、
SelectedPaths[]

を受け取る。
各Pathについて、
File
Directory

を判定して、
Copy
Move
Delete

を実行する。
選択された経緯が、
Mouse
Space
v
Ctrl+Click
Shift+Click

のどれであるかは知らない。

45. 追加MUST
Space で現在項目の選択状態をToggle

Spaceで非連続選択可能

vで連続範囲選択開始

v開始時に既存SelectionをBaseSelectionとして保持

v + j/k で範囲変更

再度vで範囲確定

Escで今回のv操作だけキャンセル

既存Selectionとv範囲を組み合わせ可能

複数の離れた範囲を選択可能

File / Directory混在選択可能

選択対象の種類をSelection側では判定しない

選択結果はSelectedPaths[]としてFileOperationsへ渡す

yy / dd / x を複数選択へ適用可能

Ctrl+C / Ctrl+X / Delete / Shift+Deleteも複数選択へ適用可能

xでは削除確認必須
x確認のデフォルトはNo

Deleteは確認なしでRecycle Bin

Shift+Deleteは確認ありで完全削除


46. 追加MUST NOT
完全なVim Visual Mode

Visual Line Mode
Visual Block Mode

Text Object
Operator grammar
Count prefix
Register
Macro

選択対象種別ごとのMode

File-only selection
Directory-only selection

非連続選択専用の複雑なSelection Engine

選択履歴
Selection stack

Vim Command-line Mode
Command Palette

Windowsパス自動補正

全ディスク検索
ファイルFuzzy Search

検索index
検索DB
検索cache
学習
AI補正

独自Unicode library
日本語形態素解析
読み仮名検索
ローマ字変換


47. 設計思想
選択機構は、
項目
 ↓
SelectedItems
 ↓
SelectedPaths[]
 ↓
FileOperations

という単純な流れにする。
Space は、
現在項目をToggle

するだけ。
v は、
BaseSelection
+
連続Range

を作るだけ。
File / Directoryの違い、選択方法の違い、連続 / 非連続の違いは、FileOperationsへ持ち込まない。
選択は選択
操作は操作

として責務を分離する。
vipane12がVimから借りるのは、
hjkl
Ctrl+hjkl
yy
dd
p
x
v
Space
e
c

という操作語彙だけとする。
VimそのもののMode systemやGrammarは持たない。
機能は増やしても、内部の概念は増やさない。

48. ファイル操作の安全境界
コピー・移動元と貼り付け先が同一、または相互に包含する場合は、変更前にエラーとする。
同名ディレクトリのReplace Allは統合とし、貼り付け先だけにある項目は削除しない。
同名ファイルは上書きするが、読み取り前に貼り付け先を削除しない。
ファイルとディレクトリの同名競合は、破壊的に置換せずエラーとする。
移動に成功した項目はClipboardから外し、未完了の項目だけを残す。

接合点・シンボリックリンクを経由したコピー・移動は対象外とし、エラーを表示する。
Fuzzy Jumpでは接合点・シンボリックリンクをたどらない。ドライブ全体の探索も行わない。
これらの対応のために独自のリンク解決エンジンは作らない。

従来の.NET Frameworkのパス長制限を超える貼り付け先は、変更前にエラーとする。
長いパスへの対応を暗黙の環境設定変更で有効化しない。
上書き・削除の途中でI/Oエラーになった場合のトランザクションやロールバックは実装しない。

49. 回帰テスト
test.batから、.NET Framework付属のcsc.exeだけでテストを実行できること。
tests/RegressionTests.csで、選択・キー入力・ファイル操作・Fuzzy Jump・Unicode・保存を確認する。
テストデータは実行ごとのtests/run-*配下に隔離し、成功時に削除する。
ユーザーのworkspace.txtや起動中のアプリは変更しない。

# vipane12 追加要件2

本書は `vipane12` の既存要件および「追加要件」に対する追加仕様とする。

追加対象は以下の2点。

```text
1. ステータスバー
2. Esc Esc による一時状態の全解除
```

既存のKISS / YAGNI / Least Mechanism / Explicit over Implicit方針を維持する。

---

# 1. ステータスバー

## 1.1 目的

現在のvipane12が、

```text
何をしている状態か
何を対象としているか
直前に何をしたか
```

をユーザーから確認できるようにする。

特に以下のような、画面上から判別しにくい内部状態を明示する。

```text
yy / dd の途中入力

Range Selection

Copy / Move Pending

Path Input

Fuzzy Jump

SelectionList

外部コマンド起動
```

操作履歴やログ機能は目的としない。

---

# 2. 表示位置

メインウィンドウ下部に1行のステータスバーを配置する。

ステータスバーは3領域に分ける。

```text
[ Current State ]    [ Selection / Pending ]    [ Last Action ]
```

それぞれ、

```text
左     現在状態
中央   現在の対象・Pending
右     直前の完了操作
```

を表示する。

---

# 3. Current State

左側には現在のアプリケーション状態を表示する。

基本的な表示語彙は以下とする。

```text
Ready

Waiting: y
Waiting: d

Range select

Path input

Select command

Fuzzy search: <token>
Select directory

Copying...
Moving...
Deleting...

Running: <command>

Error: <message>

Esc again: clear all
```

必要以上に状態語彙を増やさない。

---

# 4. Ready

特別な操作状態にない場合は、

```text
Ready
```

と表示する。

例：

```text
Ready    Selected: 0    Last: Refreshed
```

---

# 5. 複数キー入力待ち

Vim風複数キー入力の途中状態を表示する。

`y` を1回押した状態：

```text
Waiting: y
```

`d` を1回押した状態：

```text
Waiting: d
```

これにより、

```text
yy
dd
```

の途中状態を明示する。

---

# 6. Range Selection

`v` による範囲選択中は、

```text
Range select
```

と表示する。

中央には現在の範囲数を表示する。

例：

```text
Range select    Range: 5    Last: Refreshed
```

---

# 7. Path Input

`e` によってPathInputへフォーカスしている間は、

```text
Path input
```

と表示する。

例：

```text
Path input    Selected: 2    Last: Jumped to 30_Driver
```

---

# 8. カスタムコマンド選択

`c` によってコマンドSelectionListを表示している間は、

```text
Select command
```

と表示する。

中央には候補数を表示してよい。

例：

```text
Select command    6 commands    Last: Copied 3 items
```

---

# 9. Fuzzy Jump

Fuzzy Jumpの探索中は、

```text
Fuzzy search: <token>
```

と表示する。

例：

```text
Fuzzy search: 20ba    Selected: 0    Last: Refreshed
```

複数候補からの選択中は、

```text
Select directory
```

と表示する。

中央には候補数を表示する。

例：

```text
Select directory    3 candidates    Last: Refreshed
```

---

# 10. ファイル操作中

vipane12自身がファイル操作を実行している間は以下を表示する。

```text
Copying...
Moving...
Deleting...
```

例：

```text
Copying...    Copy pending: 4    Last: Copied selection
```

非同期ジョブ管理や進捗率表示は実装しない。

---

# 11. 外部コマンド

カスタムコマンドを起動するときは、

```text
Running: <command>
```

と表示してよい。

例：

```text
Running: SVN Update    Selected: 1    Last: Copied 3 items
```

外部プロセスの終了状態までは追跡しない。

プロセスを正常に起動できた時点でvipane12の責務完了とする。

---

# 12. Selection / Pending

中央には現在の選択またはPending状態を表示する。

基本表示：

```text
Selected: N
```

範囲選択中：

```text
Range: N
```

Copy Pending：

```text
Copy pending: N
```

Move Pending：

```text
Move pending: N
```

SelectionList：

```text
N candidates
```

または、

```text
N commands
```

とする。

---

# 13. Selection表示

通常状態では、

```text
Selected: N
```

と表示する。

例：

```text
Ready    Selected: 4    Last: Refreshed
```

選択数が0の場合も、

```text
Selected: 0
```

と明示してよい。

---

# 14. Copy Pending

`yy` または `Ctrl+C` によって内部Copy Pendingが存在する場合、

```text
Copy pending: N
```

と表示する。

例：

```text
Ready    Copy pending: 4    Last: Copied selection
```

---

# 15. Move Pending

`dd` または `Ctrl+X` によって内部Move Pendingが存在する場合、

```text
Move pending: N
```

と表示する。

例：

```text
Ready    Move pending: 3    Last: Move selection
```

---

# 16. Last Action

右側には直前に完了した操作を1件だけ表示する。

形式：

```text
Last: <short summary>
```

例：

```text
Last: Copied 4 items

Last: Moved 2 items

Last: Deleted 3 items

Last: Opened main.c

Last: Refreshed

Last: SVN Update

Last: Jumped to 30_Driver
```

---

# 17. Last Actionの制約

保持する履歴は直近1件のみとする。

以下は実装しない。

```text
履歴一覧
履歴スタック
日時付き履歴
ログファイル
ログビューア
操作履歴検索
```

Last Actionはメモリ上の単純な文字列程度でよい。

---

# 18. 長いパス

ステータスバーへ長いフルパスを表示しない。

例えば、

```text
C:\SVN\ProjectA\Software\Driver\Module\30_Driver
```

へJumpした場合、

```text
Last: Jumped to 30_Driver
```

程度の短い表示とする。

各ペインにCurrentPath表示が存在するため、ステータスバーへ重複してフルパスを表示しない。

---

# 19. エラー表示

エラー発生時はCurrent Stateを、

```text
Error: <message>
```

とする。

必要に応じてステータスバー全体を短いエラー表示として使用してよい。

例：

```text
Error: Copy failed | Access denied | foo.txt
```

既存仕様どおり重要なファイル操作エラーではダイアログも表示する。

ステータスバーはダイアログの代替にはしない。

---

# 20. 表示優先順位

ステータス表示の優先順位は以下とする。

```text
1. Error
2. 現在進行中の状態
3. Pending状態
4. Last Action
5. Ready
```

現在状態が存在する場合、Last Actionより現在状態を優先して表示する。

---

# 21. ステータスバーの責務

ステータスバーは状態を、

```text
表示するだけ
```

とする。

ステータスバー自体から操作は行わない。

クリック可能なボタンやメニューは配置しない。

---

# 22. ステータスバー内部状態

概念的には以下程度とする。

```text
CurrentState
LastAction
```

Selection数やPending数は既存状態から取得する。

ステータスバー専用に状態を二重管理しない。

---

# 23. ステータスバー MUST NOT

以下は実装しない。

```text
操作履歴一覧

ログビューア
ログファイル

通知センター

進捗パネル

コピーキュー表示

タイムスタンプ履歴

ステータスバー設定画面

任意レイアウト

ユーザー定義ステータス項目

複雑な色分け設定
```

ステータスバーは常に1行とする。

---

# 24. Esc の基本動作

`Esc` は、

**現在の局所的な一時状態をキャンセルする**

キーとする。

一回のEscでは、可能な限り現在最前面の一時状態だけを解除する。

---

# 25. Range Selection中のEsc

`v` によるRange Selection中にEscを押した場合、

今回の範囲選択をキャンセルする。

```text
Selection
    = BaseSelection

RangeSelecting
    = false
```

とする。

既存のSpace等によるSelectionは維持する。

---

# 26. PathInput中のEsc

PathInput中にEscを押した場合、

```text
入力内容を破棄
CurrentPathは変更しない
FileListへフォーカスを戻す
```

とする。

---

# 27. SelectionList中のEsc

カスタムコマンド選択またはFuzzy候補選択中にEscを押した場合、

```text
SelectionListを閉じる
選択結果なし
```

として元のFileListまたは呼び出し元へ戻る。

---

# 28. PendingKey中のEsc

以下の状態でEscを押した場合、

```text
Waiting: y
Waiting: d
```

PendingKeyを解除する。

```text
PendingKey = None
```

とする。

---

# 29. Esc Esc

Escを押した直後に、他のキー入力を挟まず再度Escを押した場合、

**すべての一時UI状態を解除する。**

これを、

```text
Esc Esc
```

と定義する。

---

# 30. Esc Escの対象

Esc Escでは以下を解除する。

```text
RangeSelection状態

SelectedItems

BaseSelection

SelectionAnchor

PendingKey

Copy Pending

Move Pending

SelectionList

PathInput

Esc連続待ち状態
```

解除後は、

```text
Ready

Selected: 0

Copy Pendingなし

Move Pendingなし

PendingKeyなし
```

の状態へ戻す。

---

# 31. Esc Escで解除しないもの

Esc Escは既に完了したファイル操作をUndoしない。

以下は対象外。

```text
既に完了したCopy

既に完了したMove

既に完了したDelete

既に実行した外部コマンド

CurrentPath

Workspace

Last Action
```

Esc Escは、

**一時的なUI状態とPending操作だけを解除する。**

---

# 32. Last ActionとEsc Esc

Esc Escを実行しても、

```text
Last Action
```

は消去しない。

例：

Esc Esc前：

```text
Range select    Range: 5    Last: Copied 3 items
```

Esc Esc後：

```text
Ready    Selected: 0    Last: Copied 3 items
```

とする。

---

# 33. Esc連続判定

Esc Escの判定に時間制限を使用しない。

タイマーは実装しない。

判定規則は、

```text
直前に処理したキーがEsc
かつ
現在のキーもEsc
```

のみとする。

---

# 34. Esc以外のキー入力

Escを1回押した後、Esc以外のキーが入力された場合、

Esc連続待ち状態を解除する。

例：

```text
Esc
j
```

の場合、

```text
Esc Esc
```

とは扱わない。

`j` は通常のカーソル移動として処理する。

---

# 35. Esc連続待ち表示

Escを1回押し、Esc Escが成立可能な状態ではCurrent Stateへ、

```text
Esc again: clear all
```

と表示してよい。

例：

```text
Esc again: clear all    Selected: 3    Last: Copied 2 items
```

次にEscを押せば全解除する。

別のキーを押した場合は通常状態へ戻る。

---

# 36. Esc Escの実装状態

Esc Escのためにタイマーや専用入力システムを追加しない。

既存のPendingKey相当の状態へ、

```text
Escape
```

を追加するか、

```text
EscapePending : bool
```

程度の単純な状態を持つ。

---

# 37. 状態遷移例：Range Selection

初期：

```text
Ready    Selected: 2    Last: Refreshed
```

`v`：

```text
Range select    Range: 2    Last: Refreshed
```

`jjj`：

```text
Range select    Range: 5    Last: Refreshed
```

Esc：

```text
Esc again: clear all    Selected: 2    Last: Refreshed
```

さらにEsc：

```text
Ready    Selected: 0    Last: Refreshed
```

---

# 38. 状態遷移例：Copy Pending

初期：

```text
Ready    Selected: 4    Last: Refreshed
```

`yy`：

```text
Ready    Copy pending: 4    Last: Copied selection
```

Esc：

```text
Esc again: clear all    Copy pending: 4    Last: Copied selection
```

Esc：

```text
Ready    Selected: 0    Last: Copied selection
```

Copy Pendingは破棄される。

---

# 39. 状態遷移例：PathInput

```text
Path input    Selected: 3    Last: Refreshed
```

Esc：

```text
Esc again: clear all    Selected: 3    Last: Refreshed
```

Esc：

```text
Ready    Selected: 0    Last: Refreshed
```

---

# 40. 状態遷移例：PendingKey

`y`：

```text
Waiting: y    Selected: 4    Last: Refreshed
```

Esc：

```text
Esc again: clear all    Selected: 4    Last: Refreshed
```

さらにEsc：

```text
Ready    Selected: 0    Last: Refreshed
```

---

# 41. キーバインドへの追加

既存操作へ以下を追加する。

```text
Esc
    現在の局所状態をキャンセル

Esc Esc
    すべての一時UI状態とPending操作を解除
```

Esc EscはUndoではない。

---

# 42. 内部構造

追加後も独立した大規模なState Managerは原則作らない。

概念的には既存状態に以下を追加する程度とする。

```text
CurrentState
LastAction
EscapePending
```

既存状態：

```text
SelectedItems
RangeSelecting
SelectionAnchor
BaseSelection
PendingKey
PendingOperation
```

を再利用する。

---

# 43. ClearTransientState

Esc Esc用に、

```text
ClearTransientState()
```

相当の単純な処理を1か所へまとめてよい。

責務：

```text
RangeSelection解除

Selection解除

PendingKey解除

Copy / Move Pending解除

SelectionList解除

PathInput解除

EscapePending解除
```

とする。

既に完了したファイル操作やCurrentPathは変更しない。

---

# 44. 追加MUST

```text
画面下部に1行のStatus Barを表示

Status Barは3領域

Current State
Selection / Pending
Last Action

Current Stateに現在の操作状態を表示

yy / dd途中状態を表示

Range Selection状態を表示

Path Input状態を表示

Fuzzy Jump状態を表示

SelectionList状態を表示

Copy / Move / Delete中を表示

外部コマンド起動状態を表示

Selection数を表示

Copy Pending数を表示

Move Pending数を表示

Last Actionは直近1件のみ

Escで現在の局所状態をCancel

Esc Escですべての一時状態を解除

Esc EscでSelectedItemsを解除

Esc EscでCopy / Move Pendingを解除

Esc EscでPendingKeyを解除

Esc EscはUndoを行わない

Esc Esc判定にタイマーを使用しない

Esc以外のキー入力でEsc連続状態を解除
```

---

# 45. 追加MUST NOT

```text
操作履歴一覧

ログシステム

ログファイル

通知センター

コピーキュー

進捗率管理

Status Bar設定画面

複雑なStatus Bar Widget

Esc Esc用タイマー

Undoシステム

Transaction

Rollback

State history

専用の巨大なState Manager
```

---

# 46. 設計思想

ステータスバーは、

```text
内部状態
 ↓
短い文字列表現
 ↓
1行表示
```

とする。

新しい状態を作るための機能ではなく、

**既に存在する状態を見えるようにする機能**

として扱う。

Escは、

```text
Esc
 ↓
現在状態だけCancel
```

Esc Escは、

```text
Esc
Esc
 ↓
ClearTransientState()
 ↓
Ready
```

とする。

これにより、

```text
何をしているか分からない
何が選択されているか分からない
Copy / Move Pendingが残っているか分からない
```

という状態を避ける。

ステータスバーによって状態を明示し、

Esc Escによって一時状態を確実に捨てられる、

**見える状態 + 単純なリセット**

を提供する。

機能追加によって複雑な状態管理システムを作らない。

