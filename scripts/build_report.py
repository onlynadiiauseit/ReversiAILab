"""Rebuild the Ukrainian lab report from verified experiment artifacts.

Requires python-docx and Pillow. Run from the repository root after experiments.
"""

import csv
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from docx import Document
from docx.shared import Cm, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[1]
RESULTS = ROOT / 'results'
ASSETS = ROOT / 'docs/assets'
ASSETS.mkdir(parents=True, exist_ok=True)
summary = json.loads((RESULTS / 'summary.json').read_text(encoding='utf-8-sig'))
environment = json.loads((RESULTS / 'environment.json').read_text(encoding='utf-8-sig'))
trace = json.loads((RESULTS / 'tree_midgame.json').read_text(encoding='utf-8-sig'))
raw = list(csv.DictReader((RESULTS / 'experiments.csv').open(encoding='utf-8-sig')))
assert len(raw) == 432
test_output = (RESULTS / 'tests.txt').read_text(encoding='utf-8-sig')
assert 'ALL 200 TESTS PASSED' in test_output


def result(position, depth, order='BestFirst', evaluation='Positional'):
    return next(
        x
        for x in summary
        if (x['Position'], x['Depth'], x['Ordering'], x['Evaluation'])
        == (position, depth, order, evaluation)
    )


def number(n):
    return f'{n:,}'.replace(',', ' ')


def font(size, bold=False):
    filename = 'arialbd.ttf' if bold else 'arial.ttf'
    return ImageFont.truetype(str(Path('C:/Windows/Fonts') / filename), size)


# A conventional chart, generated from actual measurements.
image = Image.new('RGB', (1500, 610), 'white')
draw = ImageDraw.Draw(image)
draw.text((35, 20), 'Середня позиція   глибина 5   Positional', fill='black', font=font(32, True))
entries = [('Minimax', result('midgame', 5)['MinimaxNodes'], '#707070')]
entries += [
    (o, result('midgame', 5, o)['AlphaBetaNodes'], '#256347')
    for o in ['Natural', 'BestFirst', 'WorstFirst']
]
maximum = max(x[1] for x in entries)
for i, (label, count, color) in enumerate(entries):
    y = 110 + i * 110
    draw.text((35, y + 10), label, fill='black', font=font(29))
    width = round(count / maximum * 970)
    draw.rectangle((265, y, 265 + max(width, 2), y + 52), fill=color)
    draw.text((280 + width, y + 8), number(count), fill='black', font=font(27))
draw.text(
    (265, 565),
    'Відвідані вузли   менше значення означає менший перебір',
    fill='#444444',
    font=font(24),
)
image.save(ASSETS / 'nodes_midgame.png')

doc = Document()
section = doc.sections[0]
section.page_height, section.page_width = Cm(29.7), Cm(21)
section.top_margin = section.bottom_margin = Cm(1.8)
section.left_margin = section.right_margin = Cm(2)
normal = doc.styles['Normal']
normal.font.name = 'Times New Roman'
normal.font.size = Pt(12)
normal.paragraph_format.line_spacing = 1.12
normal.paragraph_format.space_after = Pt(7)
for name, size in [('Title', 23), ('Subtitle', 15), ('Heading 1', 16), ('Heading 2', 13)]:
    style = doc.styles[name]
    style.font.name = 'Times New Roman'
    style.font.size = Pt(size)
    style.font.color.rgb = RGBColor(0, 0, 0)
    style.paragraph_format.space_before = Pt(12)
    style.paragraph_format.space_after = Pt(8)
    style.paragraph_format.keep_with_next = True
# Word's default template can carry theme fonts and a blue Title border.
for style in doc.styles:
    for border in list(style.element.iter(qn('w:pBdr'))):
        border.getparent().remove(border)
for name in ['Normal', 'Title', 'Subtitle', 'Heading 1', 'Heading 2']:
    style = doc.styles[name]
    fonts = style.element.get_or_add_rPr().get_or_add_rFonts()
    for attribute in list(fonts.attrib):
        if attribute.endswith('Theme'):
            del fonts.attrib[attribute]
    for script in ['ascii', 'hAnsi', 'eastAsia', 'cs']:
        fonts.set(qn('w:' + script), 'Times New Roman')
    style.font.italic = False
doc.core_properties.title = 'Ігровий пошук у Reversi'
doc.core_properties.author = 'Черниченко Надія'
doc.core_properties.subject = 'Лабораторна робота 3 Абстрактні стратегічні ігри'
footer = section.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
field = OxmlElement('w:fldSimple')
field.set(qn('w:instr'), 'PAGE')
footer._p.append(field)


def p(text, style=None):
    return doc.add_paragraph(text, style)


def h(text):
    return doc.add_heading(text, level=1)


def page(title):
    doc.add_page_break()
    h(title)


def table(headers, rows, widths=None, size=10):
    t = doc.add_table(rows=1, cols=len(headers))
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    if widths:
        for c, width in zip(t.columns, widths):
            c.width = Cm(width)
    for i, text in enumerate(headers):
        t.rows[0].cells[i].text = str(text)
    for row in rows:
        cells = t.add_row().cells
        for i, text in enumerate(row):
            cells[i].text = str(text)
    for ri, row in enumerate(t.rows):
        props = row._tr.get_or_add_trPr()
        avoid = OxmlElement('w:cantSplit')
        props.append(avoid)
        if ri == 0:
            repeat = OxmlElement('w:tblHeader')
            props.append(repeat)
        for ci, cell in enumerate(row.cells):
            if widths:
                cell.width = Cm(widths[ci])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            tcpr = cell._tc.get_or_add_tcPr()
            borders = OxmlElement('w:tcBorders')
            for side in ['top', 'left', 'bottom', 'right']:
                edge = OxmlElement('w:' + side)
                for k, v in [('val', 'single'), ('sz', '5'), ('color', 'D9D9D9')]:
                    edge.set(qn('w:' + k), v)
                borders.append(edge)
            tcpr.append(borders)
            shade = OxmlElement('w:shd')
            shade.set(
                qn('w:fill'), 'E8E8E8' if ri == 0 else ('F7F7F7' if ri % 2 == 0 else 'FFFFFF')
            )
            tcpr.append(shade)
            margins = OxmlElement('w:tcMar')
            for side in ['top', 'left', 'bottom', 'right']:
                margin = OxmlElement('w:' + side)
                margin.set(qn('w:w'), '85')
                margin.set(qn('w:type'), 'dxa')
                margins.append(margin)
            tcpr.append(margins)
            for para in cell.paragraphs:
                para.paragraph_format.space_after = Pt(2)
                para.paragraph_format.line_spacing = 1.05
                para.alignment = WD_ALIGN_PARAGRAPH.LEFT if ci == 0 else WD_ALIGN_PARAGRAPH.CENTER
                for run in para.runs:
                    run.font.size = Pt(size)
                    run.bold = ri == 0
    p('').paragraph_format.space_after = Pt(0)
    return t


p('Київський національний університет імені Тараса Шевченка').alignment = WD_ALIGN_PARAGRAPH.CENTER
p('Лабораторна робота 3', 'Subtitle').alignment = WD_ALIGN_PARAGRAPH.CENTER
p('Абстрактні стратегічні ігри', 'Subtitle').alignment = WD_ALIGN_PARAGRAPH.CENTER
p('Ігровий пошук у Reversi', 'Title').alignment = WD_ALIGN_PARAGRAPH.CENTER
p('Minimax та alpha beta відтинання', 'Subtitle').alignment = WD_ALIGN_PARAGRAPH.CENTER
p('Виконала Черниченко Надія\nГрупа ТТП-42').alignment = WD_ALIGN_PARAGRAPH.RIGHT
p('Київ 2026').alignment = WD_ALIGN_PARAGRAPH.CENTER
h('Результат роботи')
p(
    'Реалізовано гру Reversi на дошці 8 × 8, два алгоритми пошуку, дві евристичні оцінки та три порядки обходу ходів. Застосунок Windows Forms підтримує гру, порівняння алгоритмів, скасування обчислень і експорт дерева пошуку.'
)
p(
    'Проведено 432 вимірювання на трьох відтворюваних позиціях. У всіх парних вимірюваннях minimax та alpha-beta дали однакову кореневу оцінку. Пройдено 200 автоматичних перевірок правил, пошуку й трасування. Числові таблиці нижче обчислено з results/experiments.csv.'
)
p(
    'Мета дослідження — перевірити точність alpha-beta відносно повного перебору заданої глибини та виміряти, як порядок ходів і функція оцінки впливають на обсяг роботи. Якість гри в турнірних умовах окремо не вимірювалася.'
)

page('1 Модель гри та архітектура')
p(
    'Reversi — детермінована гра двох суперників із повною інформацією. Чорні починають. Хід ставить фішку на порожню клітинку й має замкнути принаймні одну непорожню безперервну лінію фішок суперника між новою та власною фішками. Перевіряються всі вісім напрямків; перевертаються всі замкнені лінії [1].'
)
p(
    'Якщо поточна сторона не має допустимого ходу, вона обов’язково пропускає чергу. Довільний пропуск за наявності ходу заборонено. Партія завершується, коли обидві сторони не мають ходів; при цьому на дошці можуть залишатися порожні клітинки. Переможець має більше фішок.'
)
p(
    'Стан S = (C, t), де C — масив 64 значень із множини {−1, 0, +1}, t ∈ {−1, +1}. Чорні B = +1, білі W = −1. Множина A(S) містить допустимі клітинки; за вимушеного пропуску A(S) = {PASS}. У термінальному стані наступних дій немає.'
)
p(
    'Функція переходу T(S, a) створює незалежну копію дошки, застосовує захоплення та змінює сторону. Властивість Cells надає лише читання, тому зовнішній код не може змінити стан. Лічильник Passes є допоміжною статистикою; завершеність визначається можливістю ходів обох сторін.'
)
table(
    ['Модуль', 'Призначення'],
    [
        ['Reversi.Core', 'Правила, UF і HEF, minimax, alpha-beta, метрики та журнал'],
        ['Reversi.Desktop', 'Дошка, налаштування, фоновий пошук, скасування, JSON'],
        ['Reversi.Tests', 'Консольні перевірки без стороннього тестового фреймворку'],
        ['Reversi.Experiments', 'Пакетні запуски, CSV, зведення та опис середовища'],
        ['scripts/build_report.py', 'Відтворення звіту з перевірених результатів'],
    ],
    [4.7, 12.3],
    11,
)
p(
    'Генератор допустимих ходів перевіряє захоплення без створення тимчасових списків для кожної порожньої клітинки. HasLegalMove завершує перевірку на першому знайденому ході. Повний список перевертань будується під час виконання ходу.'
)

page('2 Алгоритми пошуку')
p(
    'MAX — сторона, яка ходить у корені поточного пошуку. Усі оцінки обчислюються від її імені. У вузлі MAX береться найбільше значення нащадків, у MIN — найменше. Для термінальної позиції використовується точна UF; для незавершеної позиції на межі глибини — HEF [2].'
)
p(
    'V(S, d) = UF(S), якщо S термінальний; V(S, 0) = HEF(S); інакше V(S, d) = max V(T(S, a), d − 1) для MAX або min для MIN. Вимушений PASS також зменшує d на одиницю. Ця конвенція однакова в обох алгоритмах.'
)
p(
    'Alpha-beta передає нижню межу α та верхню межу β. У вузлі MAX оновлюється α = max(α, best), у MIN — β = min(β, best). Якщо α ≥ β, наступні діти не можуть поліпшити рішення відповідного предка, тому їх можна пропустити без зміни minimax-оцінки кореня [2].'
)
code = p(
    'Search(S, d, α, β)\n  якщо terminal(S): повернути UF(S)\n  якщо d = 0: повернути HEF(S)\n  якщо немає ходів: Search(PASS(S), d−1, α, β)\n  для кожного впорядкованого ходу a:\n    value = Search(T(S,a), d−1, α, β)\n    оновити best та α або β\n    якщо α ≥ β і залишилися діти: перервати\n  повернути best'
)
for run in code.runs:
    run.font.name = 'Consolas'
    run.font.size = Pt(10)
p(
    'За однакової глибини та HEF обидва алгоритми мають повертати однакове значення кореня. За рівності кількох оптимальних ходів зберігається перший у поточному порядку. Відмінність обраних клітинок сама по собі не означає помилку.'
)
p(
    'Nodes рахує всі відвідані виклики, включно з коренем і PASS. Leaves рахує термінальні вузли та вузли на межі глибини. Cutoffs збільшується лише тоді, коли умова α ≥ β справді пропускає хоча б одного наступного нащадка.'
)
p(
    'Пошук має експоненційну залежність від глибини у загальному випадку. Сортування потребує додаткових оцінок дочірніх позицій; ці обчислення входять у час пошуку, але не в Nodes. Тому зменшення Nodes не тотожне прискоренню у стільки ж разів.'
)

page('3 Функції оцінки та порядок ходів')
p(
    'Нехай ΔD = count(MAX) − count(MIN). Для кінцевого стану UF = 100000 + ΔD при виграші, UF = −100000 + ΔD при програші, UF = 0 при нічиїй. Велика константа відокремлює результат завершеної гри від звичайної статичної оцінки.'
)
p(
    'Базова HEF DiscDifference дорівнює ΔD. Розширена HEF Positional задається формулою: ΣwᵢCᵢMAX + 25ΔM + 45ΔK + 5ΔP + kΔD. Тут ΔM — різниця кількості допустимих ходів; ΔK — різниця зайнятих кутів; ΔP — різниця кількості порожніх клітинок, суміжних із фішками суперника. Кожна така клітинка рахується один раз.'
)
p(
    'Коефіцієнт k дорівнює 1 за кількості зайнятих клітинок менше 44, 3 для 44–55 і 12 для 56–64. Ваги є заданими евристичними припущеннями; їх не навчали й не оптимізували на ігрових серіях.'
)
weights = [
    120,
    -20,
    20,
    5,
    5,
    20,
    -20,
    120,
    -20,
    -40,
    -5,
    -5,
    -5,
    -5,
    -40,
    -20,
    20,
    -5,
    15,
    3,
    3,
    15,
    -5,
    20,
    5,
    -5,
    3,
    3,
    3,
    3,
    -5,
    5,
    5,
    -5,
    3,
    3,
    3,
    3,
    -5,
    5,
    20,
    -5,
    15,
    3,
    3,
    15,
    -5,
    20,
    -20,
    -40,
    -5,
    -5,
    -5,
    -5,
    -40,
    -20,
    120,
    -20,
    20,
    5,
    5,
    20,
    -20,
    120,
]
p('Таблиця 1 Позиційні ваги клітинок від A1 до H8')
table(
    ['Рядок'] + list('ABCDEFGH'),
    [[str(r + 1)] + weights[r * 8 : r * 8 + 8] for r in range(8)],
    [2.2] + [1.85] * 8,
    10,
)
p(
    'Кути мають високу вагу; сусідні клітинки штрафуються фіксованими коефіцієнтами. Цей штраф застосовується завжди, навіть коли кут уже зайнятий. Статична таблиця не враховує весь тактичний контекст позиції.'
)
p(
    'Natural перебирає клітинки рядок за рядком. BestFirst сортує всі дочірні позиції за HEF: у MAX за спаданням, у MIN за зростанням. WorstFirst використовує зворотний порядок. За рівності оцінок діє порядок рядка та стовпця. Сортування не вилучає ходи; пошук зберігає точність для обраної глибини.'
)

page('4 Протокол експериментів')
p(
    'Вимірювання виконано 13 листопада 2026 року у конфігурації Release без відлагоджувача. Позиції створено легальною детермінованою партією: на переході turn обирається legalMoves[(turn × 7 + 3) mod legalMoves.Count], де turn починається з нуля. Якщо ходів немає, виконується PASS.'
)
table(
    ['Параметр', 'Значення'],
    [
        ['Позиції', 'opening — початок; midgame — після 22 переходів; endgame — після 50'],
        ['Глибина', '2, 3, 4, 5 переходів, включно з PASS'],
        ['Алгоритми', 'Minimax та Alpha-Beta'],
        ['Оцінки та порядок', 'DiscDifference / Positional; Natural / BestFirst / WorstFirst'],
        ['Повторення', '3 для кожної комбінації; всього 432 вимірювання'],
        ['Прогрів', 'Окремий неврахований запуск кожного методу для кожної конфігурації'],
        ['Послідовність', 'Порядок двох методів чергується між повтореннями'],
        ['Обладнання', environment.get('CPU', 'AMD Ryzen 5 7430U with Radeon Graphics')],
        [
            'Середовище',
            f"{environment['OS']}; {environment['Framework']}; {environment['Architecture']}",
        ],
        [
            'SDK і паралелізм',
            f".NET SDK {environment.get('SDK', '10.0.401')}; {environment['LogicalProcessors']} логічних процесорів",
        ],
    ],
    [4.1, 12.9],
    10,
)
p(
    'Для кожної конфігурації перевіряється збіг Score з minimax, а також Nodes_AB ≤ Nodes_Minimax. Час вимірюється Stopwatch лише всередині FindBest; запис файлів до нього не входить. У таблицях часу використано медіану трьох повторень.'
)
p(
    'R = 100 × (1 − Nodes_AB / Nodes_Minimax) — відсоток скорочення вузлів. Відношення Nodes_Minimax / Nodes_AB характеризує скорочення перебору, а T_Minimax / T_AB — фактичне прискорення. Час залежить від JIT, GC і фонових процесів. Три повторення не дають надійної оцінки статистичної похибки.'
)
p(
    'Первинні дані містяться в experiments.csv; агрегати — у summary.json; точні позиції — у positions.txt; умови запуску — в environment.json. Файли зберігаються у каталозі results.'
)

page('5 Виміряний вплив порядку ходів')
p('Таблиця 2 Пошук на глибині 5 з оцінкою Positional')
rows = []
for pos in ['opening', 'midgame', 'endgame']:
    for order in ['Natural', 'BestFirst', 'WorstFirst']:
        x = result(pos, 5, order)
        rows.append(
            [
                pos,
                order,
                number(x['MinimaxNodes']),
                number(x['AlphaBetaNodes']),
                f"{x['ReductionPercent']:.1f}",
                f"{x['MinimaxMedianMs']:.2f}",
                f"{x['AlphaBetaMedianMs']:.2f}",
            ]
        )
table(
    ['Позиція', 'Порядок', 'MM\nвузлів', 'AB\nвузлів', 'R\n%', 'MM\nмс', 'AB\nмс'],
    rows,
    [2.3, 2.7, 2.7, 2.7, 1.5, 2.55, 2.55],
    9,
)
doc.add_picture(str(ASSETS / 'nodes_midgame.png'), width=Cm(16.8))
p('Рисунок 1 Вузли середньої позиції за однакової глибини та HEF')
x = result('midgame', 5)
w = result('midgame', 5, 'WorstFirst')
p(
    f"У середній позиції BestFirst скорочує перебір із {number(x['MinimaxNodes'])} до {number(x['AlphaBetaNodes'])} вузлів, тобто на {x['ReductionPercent']:.2f}%. Для WorstFirst відвідано {number(w['AlphaBetaNodes'])} вузлів. Усі ці варіанти мають однакову оцінку кореня {x['Score']}."
)
p(
    'Ефект упорядкування залежить від позиції. Назви BestFirst і WorstFirst описують порядок статичної евристики, а не гарантований порядок істинних minimax-значень. Тому BestFirst не зобов’язаний бути найкращим у кожному окремому випадку.'
)

page('6 Глибина та вибір евристики')
p('Таблиця 3 Середня позиція з порядком BestFirst')
rows = []
for depth in [2, 3, 4, 5]:
    for evaluation in ['DiscDifference', 'Positional']:
        x = result('midgame', depth, evaluation=evaluation)
        move = next(
            r['best_move']
            for r in raw
            if r['position'] == 'midgame'
            and int(r['depth']) == depth
            and r['evaluation'] == evaluation
            and r['ordering'] == 'BestFirst'
            and r['method'] == 'alphabeta'
        )
        rows.append(
            [
                depth,
                evaluation,
                move,
                x['Score'],
                number(x['MinimaxNodes']),
                number(x['AlphaBetaNodes']),
                f"{x['ReductionPercent']:.1f}",
            ]
        )
table(
    ['d', 'HEF', 'Хід', 'Score', 'MM вузлів', 'AB вузлів', 'R %'],
    rows,
    [0.7, 3.8, 1.2, 1.7, 3.4, 3.4, 2.8],
    9,
)
p(
    'Зі збільшенням глибини повний minimax розглядає більше вузлів. Alpha-beta також має дослідити достатню частину дерева для підтвердження оцінки; частка відсічених гілок залежить від того, наскільки вдало HEF передбачає порядок сильних ходів.'
)
p(
    'Значення Score різних HEF не слід порівнювати як єдину шкалу якості: DiscDifference вимірює різницю фішок, а Positional — зважену суму кількох ознак. Зміна ходу при іншій глибині або оцінці демонструє вплив припущень пошуку, але сама по собі не доводить поліпшення гри.'
)
counter = [
    x
    for x in summary
    if x['Evaluation'] == 'Positional'
    and x['Ordering'] == 'BestFirst'
    and x['AlphaBetaNodes'] > result(x['Position'], x['Depth'], 'Natural')['AlphaBetaNodes']
]
if counter:
    c = counter[0]
    natural = result(c['Position'], c['Depth'], 'Natural')
    p(
        f"Конкретний виняток: {c['Position']}, d = {c['Depth']}, Positional. BestFirst відвідав {number(c['AlphaBetaNodes'])} вузлів, Natural — {number(natural['AlphaBetaNodes'])}. Додаткове сортування за локальною HEF не гарантує меншого дерева на глибині кількох переходів."
    )
p(
    'Перевірка всіх 432 рядків підтвердила збіг оцінок у відповідних парах методів. Вузлові лічильники стабільні для повторів однієї конфігурації; часові значення можуть відрізнятися. Для порівняння сили HEF потрібна окрема серія повних партій з обміном кольорами та однаковими обмеженнями ресурсів.'
)

page('7 Фактичний приклад відтинання')
p(
    'Для midgame виконано Alpha-Beta з d = 3, BestFirst та Positional. Журнал tree_midgame.json містить усі відвідані вузли цієї демонстрації. Записи зберігаються після завершення рекурсивного виклику; Id призначається при вході, Parent вказує батьківський вузол.'
)
cut = next(x for x in trace if x['Cutoff'])
lookup = {x['Id']: x for x in trace}
path = []
node = cut
while True:
    path.append(node)
    if node['Parent'] is None:
        break
    node = lookup[node['Parent']]
path.reverse()
p('Таблиця 4 Шлях від кореня до вузла з реальним відтинанням')
bound = lambda v: '−∞' if v == -2147483648 else '+∞' if v == 2147483647 else str(v)
table(
    ['Id', 'Parent', 'Роль', 'Хід', 'Score', 'α після', 'β після', 'CUT'],
    [
        [
            x['Id'],
            x['Parent'] if x['Parent'] is not None else '—',
            x['Player'],
            x['Move'],
            x['Score'],
            bound(x['AlphaAfter']),
            bound(x['BetaAfter']),
            'так' if x['Cutoff'] else 'ні',
        ]
        for x in path
    ],
    [1, 1.5, 1.6, 1.7, 2.1, 3.1, 3.1, 2.9],
    10,
)
p(
    f"У вузлі #{cut['Id']} ({cut['Player']}, хід {cut['Move']}) межі після обробки дорівнюють α = {bound(cut['AlphaAfter'])} та β = {bound(cut['BetaAfter'])}. Умова α ≥ β виконується, і частину наступних дітей пропущено. Score = {cut['Score']} у відсіченому внутрішньому вузлі може бути лише межею, а не його точним minimax-значенням; точне значення потрібне насамперед для кореня."
)
children = [x for x in trace if x['Parent'] == cut['Id']]
if children:
    p('Таблиця 5 Відвідані діти цього вузла в порядку входу')
    table(
        ['Id', 'Хід', 'Score', 'Причина завершення'],
        [
            [x['Id'], x['Move'], x['Score'], x['Reason']]
            for x in sorted(children, key=lambda x: x['Id'])
        ],
        [1.2, 2, 2.3, 11.5],
        10,
    )
p(
    f"У повному демонстраційному журналі збережено {len(trace)} записів, з них {sum(x['Cutoff'] for x in trace)} позначені Cutoff = true. Число Cutoffs означає події пропуску решти дітей; воно не дорівнює кількості невідвіданих вузлів або листків."
)
p(
    'Коли встановлено traceLimit, зберігаються вузли з Id < traceLimit. Завдяки цьому корінь і всі предки кожного збереженого вузла залишаються в журналі. Трасування може бути неповним, тому суму Cutoff в обрізаному JSON не можна ототожнювати з повним лічильником пошуку.'
)
p(
    'У вікні застосунку кнопка «Дерево та alpha/beta» показує текстове трасування й пропонує зберегти JSON. Для відновлення дерева потрібно використовувати Id та Parent, а не порядок рядків у файлі.'
)

page('8 Перевірка програми та інтерфейс')
p(
    'Команда dotnet run --project Reversi.Tests -c Release успішно завершила 200 перевірок. Протокол збережено в results/tests.txt. Перевірки охоплюють правила, інваріанти стану, еквівалентність пошуків і завершення партії.'
)
table(
    ['Група', 'Що перевірено'],
    [
        ['Правила', 'Початкові ходи; D3; усі 8 напрямків; межі; недопустимий хід і PASS'],
        ['Стан', 'Незмінність батьківської дошки; захист Cells; перевірка вхідних параметрів'],
        [
            'Завершення',
            'Виграш, програш, нічия; блокування з порожніми клітинками; примусовий PASS',
        ],
        ['Перебір', 'Кількість листків початку на глибинах 1–6: 4, 12, 56, 244, 1396, 8200'],
        ['Minimax та AB', 'Однакові оцінки на глибинах 1–4, за обох HEF і всіх трьох порядків'],
        [
            'Кінцеві позиції',
            '8 партій із фіксованими seed; повний перебір останніх 4 порожніх клітинок',
        ],
        ['Вибір ходу', 'У кінцевих позиціях перевірено оптимальність саме повернутого ходу'],
        ['Трасування', 'Корінь і батьки при ліміті; повне покриття; коректні межі та Cutoffs'],
        ['Скасування', 'Скасований CancellationToken перериває пошук'],
    ],
    [3.5, 13.5],
    10,
)
p(
    'Графічний застосунок виконує один хід AI за натисканням кнопки. Ручні ходи доступні обом сторонам. Позначки на дошці показують допустимі клітинки; за необхідності інтерфейс автоматично застосовує вимушений пропуск.'
)
p(
    'Пошук і порівняння виконуються через Task.Run. На час розрахунку налаштування та ручні ходи блокуються, а кнопка «Скасувати» залишається доступною. Після скасування незавершений результат не застосовується. Закриття форми також надсилає запит на скасування.'
)
p(
    'Після ручного ходу попередня траса очищується, щоб журнал не сприймався як результат пошуку для нової позиції. Розмір дошки пристосовується до розміру вікна. Автоматична перевірка збірки й консольних тестів налаштована у GitHub Actions для Windows.'
)

page('9 Відтворення та висновки')
p(
    'Для запуску потрібен .NET 10 SDK. Модулі правил, тестів і експериментів не використовують Windows Forms; графічний застосунок потребує Windows. Відкрити можна ReversiAILab.sln або Reversi.Desktop.csproj.'
)
commands = p(
    'dotnet build ReversiAILab.sln -c Release\ndotnet run --project Reversi.Tests -c Release\ndotnet run --project Reversi.Experiments -c Release -- results\ndotnet run --project Reversi.Desktop -c Release'
)
for run in commands.runs:
    run.font.name = 'Consolas'
    run.font.size = Pt(9)
p(
    'Скрипт scripts/build_report.py відтворює документ і діаграму зі збережених CSV/JSON та протоколу перевірок. Йому потрібні python-docx і Pillow. Зміни коду, налаштувань або обладнання потребують нового запуску експериментів; часові значення нового запуску можуть відрізнятися.'
)
x = result('midgame', 5)
p(
    f"Alpha-beta зберіг minimax-оцінку в усіх 432 вимірюваннях. Найпоказовіший розглянутий випадок — midgame, d = 5, Positional, BestFirst: {number(x['MinimaxNodes'])} → {number(x['AlphaBetaNodes'])} вузлів, скорочення {x['ReductionPercent']:.2f}%. Зафіксовані медіани часу для цієї конфігурації становлять {x['MinimaxMedianMs']:.2f} та {x['AlphaBetaMedianMs']:.2f} мс відповідно."
)
p(
    'Порядок ходів суттєво впливає на кількість відвіданих вузлів; локальне сортування за HEF не гарантує мінімального дерева. Позиційна оцінка враховує мобільність і кути, проте її перевагу за часткою перемог ці експерименти не встановлюють.'
)
p(
    'Обмеження роботи: фіксована дошка 8 × 8; задані вручну ваги; ефект обрію; три вибрані позиції та три часові повторення; немає таблиці транспозицій, ітеративного заглиблення й обмеження пошуку за часом. Ці можливості можуть бути наступними напрямами розвитку. Наявне скасування дозволяє користувачу вручну перервати довгий розрахунок.'
)
doc.add_heading('Джерела', level=2)
p(
    '[1] World Othello Federation. Official rules for the game Othello.\nhttps://worldothello.org/about/about-othello/othello-rules/official-rules/english'
)
p(
    '[2] University of California Berkeley. CS 188 Textbook. Minimax.\nhttps://inst.eecs.berkeley.edu/~cs188/textbook/games/minimax.html'
)
p(
    '[3] Первинні матеріали дослідження: код Reversi.Core, results/experiments.csv, summary.json, tree_midgame.json, positions.txt, environment.json та tests.txt. Дата виконання 13 листопада 2026 року.'
)
destination = ROOT / 'docs/Reversi_Звіт_Черниченко.docx'
doc.save(destination)
print(destination)
