#!/bin/sh
CURRENTFILEPATH=$0
cd ${CURRENTFILEPATH%/*}
​
FILENAME='CatBacuumCatFlying.exe'
​
MESSAGE_FAILLAUNCH="Failed to command: mono ./$FILENAME"
​
MESSAGE_FILE_NOTFOUND="Game File $FILENAME is not found."
​
MESSAGE_MONO_NOTFOUND='Command `mono` is not found.
Install with Homebrew: $brew install mono
Go to Website: https://www.mono-project.com'
​
MESSAGE_BREW_NOTFOUND='Command `brew` is not found.
Go to Website: https://brew.sh/index_ja'
​
​
function dialog () {
    osascript -e "
        tell application \"System Events\" to display alert \"$1\" buttons {\"OK\"}" #&>/dev/null
}
​
function display_alert () {
    osascript -e " 
        tell application \"System Events\"
            activate
            button returned of (display alert \"$1\" buttons {$2})
        end tell
    "
}
​
# コマンドが存在しているか
function command_exist () {
    type $1 > /dev/null 2>&1
}
​
# monoコマンドが存在しているか
if command_exist "mono"; then
    # ファイルが存在しているか
    if test -e $FILENAME; then
        if ! mono ./$FILENAME > /dev/null; then
            # 起動が失敗
            dialog "$MESSAGE_FAILLAUNCH"
        fi
    else
        dialog "$MESSAGE_FILE_NOTFOUND"
    fi
else
    # ダイアログを表示
    case $(
        display_alert "$MESSAGE_MONO_NOTFOUND" '"Cancel", "Install with Homebrew", "Go to Website"'
    ) in
        Cancel)
            false
        ;;
        # homebrewを使用してインストール
        Install\ with\ Homebrew)
            if command_exist "brew"; then
                # homebrew以外ですでに入っていると二重にインストールしてしまうので（DEBUG時に怖いから）
                if command_exist "mono"; then
                    dialog 'Command `mono` already exists.'
                else
                    brew install mono
                fi
            else
                case $(
                    display_alert "$MESSAGE_BREW_NOTFOUND" '"Cancel", "Go to Website"'
                ) in
                    Cancel)
                        false
                    ;;
                    Go\ to\ Website)
                        # ブラウザを開く
                        open "https://brew.sh/index_ja"
                    ;;
                esac
            fi
        ;;
        Go\ to\ Website)
            # ブラウザを開く
            open "https://www.mono-project.com"
        ;;
    esac
​
fi
