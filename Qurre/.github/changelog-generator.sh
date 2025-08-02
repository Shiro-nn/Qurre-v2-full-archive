#bin/bash

get_user_login() {
  local email="$1"
  local username="" # Инициализируем пустой строкой

  # Простая проверка формата email (опционально, но полезно)
  if [[ ! "$email" =~ ^.+@.+\..+$ ]]; then
    echo "::warning::Неверный формат email: $email" >&2 # Вывод предупреждения в лог Actions
    echo "" # Возвращаем пустую строку для невалидного формата
    return
  fi

  echo "::debug::Поиск пользователя для email: $email" >&2 # Отладочное сообщение
  # Ищем коммит, берем логин автора. 2>/dev/null скрывает ошибки gh (например, rate limit)
  # `|| echo ""` гарантирует пустой вывод при ошибке команды gh
  username=$(gh search commits --author-email="$email" --limit=1 --json=author --jq '.[0].author.login // empty' 2>/dev/null || echo "")

  if [[ -n "$username" ]]; then
    echo "::debug::Найден логин '$username' для '$email'" >&2
  else
    echo "::debug::Логин не найден для '$email'" >&2
  fi
  echo "$username" # Возвращаем найденный логин или пустую строку
}

VERSION=${GITHUB_REF_NAME}
echo "Generating release notes for version: $VERSION"

# Находим предыдущий тег. Если его нет (первый релиз), PREV_TAG будет пустым.
# Используем VERSION^ для поиска тега перед текущей версией.
PREV_TAG=$(git describe --tags --abbrev=0 ${VERSION}^ 2>/dev/null || echo "")

# Устанавливаем COMPARE_URL для сравнения кммитов
if [ -z "$PREV_TAG" ]; then
  echo "No previous tag found, assuming this is the first release."
  # В случае первого релиза диапазон для коммитов будет от начала истории до VERSION
  RANGE=$VERSION
  # Ссылка для сравнения будет указывать на первый коммит (можно уточнить при необходимости)
  COMPARE_URL="https://github.com/$GITHUB_REPOSITORY/commits/$VERSION"
else
  echo "Previous tag found: $PREV_TAG"
  # Диапазон коммитов для этого релиза
  RANGE="$PREV_TAG..$VERSION"
  # Ссылка для сравнения между тегами
  COMPARE_URL="https://github.com/$GITHUB_REPOSITORY/compare/$PREV_TAG...$VERSION"
fi

# 1. Получаем список коммитов для раздела "What's Changed"
# Регулярное выражение для определения строк, которые должны начинать новый элемент (стиль Conventional Commits)
# Соответствует: type:, type(scope):, type!:, type(scope)!: и т.д.
# Разрешает строчные буквы для типа, буквенно-цифровые символы/подчеркивание/пробел/дефис в области видимости (scope).
TYPE_REGEX='^[a-z]+(\([a-zA-Z0-9_ -]+\))?!?:\ ' 
# Обратите внимание на пробел в конце ^^^ для соответствия двоеточию *и* следующему пробелу для более чистого разделения.

# === Функции ===

# Вспомогательная функция для удаления начальных и конечных пробелов
trim() {
    local var="$*"
    # удалить начальные пробельные символы
    var="${var#"${var%%[![:space:]]*}"}"
    # удалить конечные пробельные символы
    var="${var%"${var##*[![:space:]]}"}"
    printf '%s' "$var"
}

# === Основная логика ===

# Инициализируем пустой массив для хранения строк вывода
declare -a output_commit_lines=()

# Использовать нулевые байты (%x00) в качестве разделителей полей для надежного парсинга вывода git log.
# Формат: HASH<null>RELATIVE_TIME<null>AUTHOR_EMAIL<null>SUBJECT_AND_BODY<null>
git log "$RANGE" --pretty=format:'%H%x00%cr%x00%ae%x00%s%x00' | \
while 
    IFS= read -r -d $'\0' HASH && \
    IFS= read -r -d $'\0' CR && \
    IFS= read -r -d $'\0' AE && \
    IFS= read -r -d $'\0' S_BODY # Считываем 4 поля для каждого коммита
do
    # Пропустить, если какое-либо важное поле пусто (не должно происходить с этим форматом)
    if [[ -z "$HASH" || -z "$CR" || -z "$AE" ]]; then
        continue
    fi
    
    # Использовать подстановку процессов и mapfile (readarray) для разделения SUBJECT_AND_BODY на строки.
    # Это правильно обрабатывает многострочные сообщения, содержащиеся в переменной S_BODY.
    # Требуется Bash 4.0+
    mapfile -t lines <<< "$S_BODY"
    
    is_first_line=true # Флаг для отслеживания самой первой строки сообщения коммита

    # Итерация по каждой строке сообщения коммита (%s)
    for line in "${lines[@]}"; do
      
        line_trimmed=$(trim "$line") # Обрезать пробелы для проверок

        # Пропустить пустые строки
        if [[ -z "$line_trimmed" ]]; then
            continue
        fi
        
        # Определить, представляет ли эта строка summary, который мы должны обработать
        process_this_line=false
        current_summary=""

        if "$is_first_line"; then
            # Первая непустая строка всегда рассматривается как summary
            current_summary="$line_trimmed"
            process_this_line=true
            is_first_line=false # Следующие строки должны соответствовать TYPE_REGEX
        # Правило 1: Проверить, начинается ли последующая строка с префикса типа conventional commit
        elif [[ "$line_trimmed" =~ $TYPE_REGEX ]]; then
             current_summary="$line_trimmed"
             process_this_line=true
        fi
        
        # Если эта строка была идентифицирована как summary для обработки...
        if "$process_this_line"; then
          
          # Правило 5: Пропустить, если идентифицированный summary начинается с '!' (например, внутренние команды),
          # но не является !breaking или !release
            if [[ "$current_summary" == "!"* && "$current_summary" != "!breaking"* && "$current_summary" != "!release"* ]]; then
                continue # Пропустить эту конкретную строку summary
            fi
            
            prefix="" # Инициализировать префикс (emoji, BREAK и т.д.)
            cleaned_summary="$current_summary" # Начать с полного summary
            
            # Правило 2: Проверить наличие префикса !breaking
            if [[ "$current_summary" == "!breaking"* ]]; then
                prefix="`BREAK` " # Обратите внимание на пробел в конце
                # Удалить префикс из summary
                cleaned_summary="${current_summary#"!breaking"}" 
                cleaned_summary=$(trim "$cleaned_summary") # Повторно обрезать после удаления префикса
            # Правило 3: Проверить наличие префикса !release
            elif [[ "$current_summary" == "!release"* ]]; then
                prefix="🎉 " # Обратите внимание на пробел в конце
                # Удалить префикс из summary
                cleaned_summary="${current_summary#"!release"}"
                cleaned_summary=$(trim "$cleaned_summary") # Повторно обрезать
            fi
            
            # Финальная обрезка содержимого summary
            cleaned_summary=$(trim "$cleaned_summary")
            
            # Получение пользователя по email
            login=$(get_user_login "$AE")
            
            # Правило 4: Отформатировать и вывести строку
            # Вывод: - {PREFIX} {SUMMARY} [(RELATIVE_TIME)](/commit/HASH) by AUTHOR_EMAIL
            formatted_line="- ${prefix}${cleaned_summary} (${CR}) by @${login} ${HASH}"
            
            # *** Добавить строку в файл ***
            echo "$formatted_line" >> commit-file.md
            
        fi # end if process_this_line
    done # end loop through lines of a single commit message
done # end loop through commits

# === Финальная обработка ===

# Теперь все строки находятся в файле "commit-file.md"

all_commit_summaries=$(cat commit-file.md)

# Теперь переменная 'all_commit_summaries' содержит все отформатированные строки,
# разделенные символами новой строки.

# Пример использования этой переменной:
echo "--- Полная Сводка Коммитов ---"
echo "$all_commit_summaries"
echo "-----------------------------"

# Вы можете использовать ${all_commit_summaries} дальше в вашем скрипте,
# например, для записи в файл или передачи другой команде.
# Пример записи в файл:
# echo "${all_commit_summaries}" > changelog_details.md


# 2. Определяем новых участников
# Получаем уникальных авторов в текущем диапазоне релиза
CURRENT_AUTHORS=$(git log --pretty=format:"%ae" $RANGE | sort -u)

if [ -z "$PREV_TAG" ]; then
  # Если это первый релиз, все авторы считаются новыми
  NEW_CONTRIBUTORS_LIST="$CURRENT_AUTHORS"
else
  # Получаем уникальных авторов во всей истории ДО предыдущего тега
  # Используем $PREV_TAG в качестве конечной точки истории "прошлых" авторов
  PAST_AUTHORS=$(git log --pretty=format:"%ae" $PREV_TAG | sort -u)
  # Находим авторов, которые есть в CURRENT_AUTHORS, но нет в PAST_AUTHORS
  # comm -13 выводит строки, уникальные для второго файла (CURRENT_AUTHORS)
  NEW_CONTRIBUTORS_LIST=$(comm -13 <(echo "$PAST_AUTHORS" | sort) <(echo "$CURRENT_AUTHORS" | sort))
fi

# Форматируем список новых участников: добавляем @ и объединяем пробелами
if [ -n "$NEW_CONTRIBUTORS_LIST" ]; then
          while IFS= read -r current_email; do
            # Пропускаем пустые строки
            if [[ -z "$current_email" ]]; then
              continue
            fi

            # Убираем возможные пробелы в начале/конце строки (если нужно)
            trimmed_email=$(echo "$current_email" | xargs)

            # Получаем логин для текущего email
            login=$(get_user_login "$trimmed_email")

            # Если логин не пустой, добавляем @логин в массив
            if [[ -n "$login" ]]; then
              processed_logins+=("@$login")
            fi
          done <<< "$NEW_CONTRIBUTORS_LIST" # Передаем содержимое NEW_CONTRIBUTORS_LIST в цикл while

          # Объединяем элементы массива через ", "
          # Работает только если массив не пустой
          CONTRIBUTORS=""
          if [[ ${#processed_logins[@]} -gt 0 ]]; then
            # Устанавливаем разделитель IFS локально для команды echo
            local IFS=", "
            # ${processed_logins[*]} использует первый символ IFS как разделитель
            CONTRIBUTORS="${processed_logins[*]}"
          fi

  echo "New contributors found: $CONTRIBUTORS"
else
  CONTRIBUTORS=""
  echo "No new contributors in this release."
fi

# 3. Составляем статистику
# Устанавливаем диапазон для diff и log
if [[ -z "$PREV_TAG" ]]; then
    echo "Предупреждение: Не найден предыдущий тег релиза. Статистика и лог будут от начала истории." >&2
    # Используем первый коммит в качестве начала диапазона
    PREV_RELEASE_REF=$(git rev-list --max-parents=0 HEAD) 
    COMMIT_RANGE="${PREV_RELEASE_REF}..${VERSION}"
    PREV_TAG_NAME="first commit"
    CALC_DAYS=false # Не можем рассчитать дни с момента первого коммита осмысленно
else
    PREV_RELEASE_REF="$PREV_TAG"
    COMMIT_RANGE="${PREV_RELEASE_REF}..${VERSION}"
    PREV_TAG_NAME="$PREV_TAG"
    CALC_DAYS=true
fi

echo "Диапазон для статистики и лога: ${COMMIT_RANGE}" >&2

# 1. Получаем статистику изменений (файлы, добавления, удаления)
DIFF_STATS=$(git diff --shortstat "$COMMIT_RANGE" 2>/dev/null)
FILES_CHANGED=$(echo "$DIFF_STATS" | grep -o '[0-9]\+ file' | grep -o '[0-9]\+')
ADDITIONS=$(echo "$DIFF_STATS" | grep -o '[0-9]\+ insertion' | grep -o '[0-9]\+')
DELETIONS=$(echo "$DIFF_STATS" | grep -o '[0-9]\+ deletion' | grep -o '[0-9]\+')

# Устанавливаем значения по умолчанию в 0, если grep ничего не нашел
FILES_CHANGED=${FILES_CHANGED:-0}
ADDITIONS=${ADDITIONS:-0}
DELETIONS=${DELETIONS:-0}

# 2. Вычисляем дни с предыдущего релиза (если он был найден)
DAYS_SINCE_STR=""
if $CALC_DAYS; then
    PREV_TIMESTAMP=$(git log -1 --format=%ct "$PREV_RELEASE_REF" 2>/dev/null)
    # Используем время коммита, на который указывает VERSION
    CURR_TIMESTAMP=$(git log -1 --format=%ct "$VERSION" 2>/dev/null) 
    
    if [[ -n "$PREV_TIMESTAMP" && -n "$CURR_TIMESTAMP" ]]; then
        DELTA_SECONDS=$(( CURR_TIMESTAMP - PREV_TIMESTAMP ))
        # Обработка случая, если предыдущий тег оказался новее (не должно быть при правильной сортировке)
        if (( DELTA_SECONDS < 0 )); then DELTA_SECONDS=0; fi 
        # Секунд в дне = 60 * 60 * 24 = 86400
        DAYS_SINCE=$(( DELTA_SECONDS / 86400 )) 
        DAYS_SINCE_STR=" | Days since (${PREV_TAG_NAME}): ${DAYS_SINCE}" 
    else
         echo "Предупреждение: Не удалось получить временные метки для расчета дней." >&2
    fi
fi

# 3. Формируем строку статистики
STATS_LINE="Files changed: ${FILES_CHANGED} | Additions: ${ADDITIONS} | Deletions: ${DELETIONS}${DAYS_SINCE_STR}"


{
  echo "# Changelog: $VERSION"
  echo ""
  
  echo "<details>"
  echo "<summary>What's Changed</summary>"
  echo ""
  echo "${all_commit_summaries}"
  echo ""
  echo "</details>"
  echo ""
  
  echo "### New Contributors"
  if [ -z "$CONTRIBUTORS" ]; then
    echo "No new contributors in this release."
  else
    echo $CONTRIBUTORS
  fi
  echo ""
  
  echo "**Full changes list**: $COMPARE_URL"
  echo ""
  
  echo "###### $STATS_LINE"
} > release-notes.md