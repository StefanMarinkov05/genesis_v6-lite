#!/usr/bin/env python3
"""Generates a large, realistic Bulgarian seed-response file for fixtures/project-spec.facenote.json
plus matching dummy product images.

Outputs (all deterministic - same RNG seed, same result):
  seeds/facenote-rich-seed.json   picked in the GUI/CLI seed picker (seeds/ is scanned by both)
  .image-cache/<provider>-<slug>.jpg
                                  pre-filled image cache. SeedStage.DownloadImageAsync checks this
                                  cache BEFORE any network call (ADR-011), so a normal run (no
                                  --image-pack, no API keys) picks these up with zero downloads.

Usage:  python3 scripts/gen-rich-seed.py            (run from anywhere; paths derive from this file)
Needs:  Pillow (images) and, for the self-check, jsonschema.

Constraints it respects (see src/Genesis.Core/Stages/SeedStage.cs BuildSeedSchema):
  - root + rows are additionalProperties:false; only parent models carry refKey
  - owner fields use "$seedUser:N" (the spec has exactly 6 seed users -> N in 0..5)
  - Product imageKeywords: 2-4 entries ("multiple" mode); files end up as product-{row}-{img}.jpg
  - Post/PostComment/Review ImagePath reuse those product files (nothing else copies images
    into wwwroot), so every image path in the seed really exists after the seed stage
"""
import json
import os
import random
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SEED_OUT = ROOT / "seeds" / "facenote-rich-seed.json"
CACHE_DIR = ROOT / ".image-cache"
PROVIDERS = ["pexels", "picsum", "pixabay"]   # cache key prefix = provider; default run uses pexels
N_USERS = 6                                   # fixtures/project-spec.facenote.json -> identity.seedUsers
rng = random.Random(2026)

# ----------------------------------------------------------------------------------------------
# Reference data
# ----------------------------------------------------------------------------------------------
BRANDS = ["Samsung", "Apple", "Xiaomi", "Huawei", "Lenovo", "Asus", "Sony", "JBL", "LG", "Philips",
          "Bosch", "Tefal", "Dyson", "Gorenje", "IKEA", "Levi's", "Zara", "Nike", "Adidas", "Puma",
          "Decathlon", "Lego", "Canon", "Hama"]

CATEGORIES = ["Мобилни телефони", "Компютри и лаптопи", "Аудио и видео", "Домакински уреди", "Мебели",
              "Дрехи", "Обувки", "Спортни стоки", "Велосипеди и тротинетки", "Играчки и игри",
              "Фотография", "Красота и здраве", "Градина и инструменти", "Умна електроника"]

# (category idx, brand, english search noun (unique per product), BG title, BG description, price BGN)
PRODUCTS = [
    # 0 Мобилни телефони
    (0, "Samsung", "samsung galaxy smartphone", "Смартфон Samsung Galaxy A54 128GB", "Със 6.4-инчов Super AMOLED дисплей и отлична батерия. Купен преди година, винаги с калъф и протектор.", 520),
    (0, "Apple", "apple iphone", "Apple iPhone 13 128GB син", "Батерия 89%, без забележки по корпуса. Включени са кутия и оригинално зарядно.", 1150),
    (0, "Xiaomi", "xiaomi redmi phone", "Xiaomi Redmi Note 12 Pro", "Камера 50MP и бързо зареждане 67W. Ползван основно у дома, дисплеят е без драскотини.", 390),
    (0, "Huawei", "huawei p30 phone", "Huawei P30 Lite 128GB", "Работи безупречно и е с Google услуги. Подходящ като резервен телефон или за по-възрастен човек.", 190),
    (0, "Samsung", "samsung galaxy tablet", "Таблет Samsung Galaxy Tab A8 10.5 инча", "Идеален за филми и онлайн уроци. Идва със стилус и калъф-книжка.", 330),
    # 1 Компютри и лаптопи
    (1, "Lenovo", "lenovo ideapad laptop", "Лаптоп Lenovo IdeaPad 3 15 инча", "Процесор Ryzen 5, 16GB RAM и 512GB SSD. Използван за учене, без следи от износване.", 880),
    (1, "Asus", "asus rog gaming laptop", "Гейминг лаптоп Asus ROG Strix", "RTX 3060, екран 144Hz и много добро охлаждане. Продава се с оригиналната чанта.", 2150),
    (1, "Apple", "apple macbook air", "Apple MacBook Air M1 256GB", "Цикли на батерията под 150. Без дефекти, с оригинално зарядно и кутия.", 1780),
    (1, "Asus", "asus computer monitor", "Монитор Asus 27 инча Full HD IPS", "Тесни рамки, 75Hz, HDMI и DisplayPort. Купен за домашен офис и вече не ми трябва.", 245),
    (1, "Lenovo", "lenovo thinkpad laptop", "Бизнес лаптоп Lenovo ThinkPad T480", "Здрав корпус и отлична клавиатура, подменена батерия миналата година. Идеален за работа.", 720),
    # 2 Аудио и видео
    (2, "Sony", "sony wireless headphones", "Безжични слушалки Sony WH-1000XM4", "Активно шумопотискане на най-високо ниво, до 30 часа работа. Меките възглавнички са като нови.", 360),
    (2, "JBL", "jbl bluetooth speaker", "Bluetooth колонка JBL Flip 6", "Водоустойчива, с мощен бас. Ползвана само няколко пъти на екскурзии.", 190),
    (2, "LG", "lg oled television", "Телевизор LG OLED 55 инча 4K", "Перфектни черни цветове, webOS и HDR. Стои на стойка, с дистанционно и кабели.", 1650),
    (2, "Philips", "philips soundbar", "Саундбар Philips с безжичен субуфер", "Подобрява значително звука на телевизора и се свързва през HDMI ARC.", 280),
    (2, "Sony", "sony bluetooth earbuds", "Слушалки тип тапи Sony WF-C500", "Леки и удобни, до 20 часа с кутията. Продавам ги заради подарена по-нова двойка.", 85),
    # 3 Домакински уреди
    (3, "Bosch", "bosch washing machine", "Пералня Bosch 8 кг 1400 оборота", "Клас А+++, тиха и икономична. Продава се заради смяна на жилището, демонтирана и готова за вземане.", 640),
    (3, "Tefal", "tefal air fryer", "Фритюрник с горещ въздух Tefal ActiFry", "Приготвя хрупкави картофи почти без мазнина. Идва с книга с рецепти.", 160),
    (3, "Dyson", "dyson cordless vacuum", "Прахосмукачка Dyson V11 безжична", "Мощно всмукване и до 60 минути работа. Включени са всички приставки и стойка за стена.", 780),
    (3, "Philips", "philips coffee machine", "Кафемашина Philips LatteGo", "Автоматична, със система за разпенване на мляко. Почистена и обезкаменена.", 590),
    (3, "Gorenje", "gorenje refrigerator", "Хладилник с фризер Gorenje 185 см", "Без скреж, енергиен клас А++. Работи безшумно и е в отлично състояние.", 520),
    # 4 Мебели
    (4, "IKEA", "ikea corner sofa", "Ъглов диван IKEA Friheten с повдигащо легло", "Сив, с просторна ракла. Калъфите се свалят и перат, удобен е за всекидневна или гостна.", 760),
    (4, "IKEA", "ikea office desk", "Бюро IKEA Bekant 160 см", "Бяла повърхност и регулируеми крачета. Идеално за работа от вкъщи.", 190),
    (4, "IKEA", "ikea bookcase", "Библиотека IKEA Billy с вратички", "Височина 202 см, цвят дъб. Сглобена е, но може да се разглоби за транспорт.", 140),
    (4, "IKEA", "ikea wardrobe", "Гардероб IKEA Pax 200 см", "Две крила, вътрешни чекмеджета и щанги. Побира много дрехи.", 410),
    (4, "IKEA", "ikea dining table", "Разтегателна маса IKEA Ingatorp със столове", "Бяла, за 4 до 6 души, с четири стола. Има леки следи от употреба.", 330),
    # 5 Дрехи
    (5, "Levi's", "levis denim jeans", "Дънки Levi's 501 тъмносини", "Размер 32/32, носени броени пъти. Класическа права кройка.", 95),
    (5, "Zara", "zara winter coat", "Дамско палто Zara от вълнена смес", "Размер М, бежово, с колан. Идеално за есен и пролет.", 110),
    (5, "Nike", "nike tracksuit", "Анцуг Nike Tech Fleece мъжки", "Размер L, сив меланж. Мек и топъл, без пилинг.", 120),
    (5, "Adidas", "adidas hoodie", "Суитшърт Adidas Originals с качулка", "Черен, размер M. Практически нов, с махнат етикет.", 65),
    (5, "Puma", "puma sports tshirt", "Тениска Puma за тренировки", "Дишаща материя, размер L. Подходяща за зала и бягане.", 30),
    # 6 Обувки
    (6, "Nike", "nike running shoes", "Маратонки Nike Air Max 270", "Номер 43, бели. Много удобни за всеки ден, леко износени отдолу.", 140),
    (6, "Adidas", "adidas sneakers", "Кецове Adidas Stan Smith", "Номер 40, кожени. Почистени и готови за носене.", 85),
    (6, "Puma", "puma football boots", "Футболни обувки Puma Future", "Номер 42, за изкуствена трева. Ползвани един сезон.", 75),
    (6, "Nike", "nike trail shoes", "Обувки за бягане Nike Pegasus Trail", "Номер 44, с добър грип по пътеки. Подметките са запазени.", 130),
    (6, "Adidas", "adidas hiking boots", "Туристически обувки Adidas Terrex", "Номер 41, водоустойчиви. Ходени на няколко прехода в Рила.", 160),
    # 7 Спортни стоки
    (7, "Decathlon", "decathlon dumbbell set", "Комплект гири Decathlon 2 x 10 кг", "Хромирани, с гумено покритие. Ползвани за домашни тренировки.", 70),
    (7, "Decathlon", "decathlon yoga mat", "Постелка за йога Decathlon 8 мм", "Нехлъзгава и лесна за почистване, с ластик за носене.", 22),
    (7, "Adidas", "adidas football", "Футболна топка Adidas официален размер 5", "Подходяща за мачове и тренировки, леко изтъркана.", 28),
    (7, "Nike", "nike gym bag", "Спортен сак Nike Brasilia 40 литра", "Здрав, с отделение за обувки. Подходящ за зала и пътуване.", 45),
    (7, "Decathlon", "decathlon camping tent", "Палатка Decathlon Quechua за 3 души", "Бързо сглобяване, водоустойчива до 2000 мм. Ползвана на две лета.", 180),
    # 8 Велосипеди и тротинетки
    (8, "Decathlon", "decathlon mountain bike", "Планински велосипед Decathlon Rockrider 27.5", "Алуминиева рамка, 21 скорости и дискови спирачки. Размер M.", 480),
    (8, "Decathlon", "decathlon city bike", "Градски велосипед Decathlon Elops", "Удобен за ежедневни пътувания, със седалка с амортисьор и багажник.", 260),
    (8, "Decathlon", "decathlon kids bike", "Детско колело Decathlon 16 инча", "За деца от 4 до 6 години, със странични колела. Малко ползвано.", 90),
    (8, "Xiaomi", "xiaomi electric scooter", "Електрическа тротинетка Xiaomi Mi Pro 2", "Пробег до 40 км и скорост 25 км/ч. Батерията е в добро състояние.", 620),
    (8, "Decathlon", "decathlon bicycle helmet", "Каска за колоездене Decathlon размер L", "С регулируема закопчалка и вентилация. Без падания и удари.", 25),
    # 9 Играчки и игри
    (9, "Lego", "lego technic set", "Конструктор Lego Technic Bugatti", "Сглобен само веднъж, пълен комплект с кутия и инструкции.", 290),
    (9, "Sony", "sony playstation console", "Конзола Sony PlayStation 5 с два джойстика", "Дисково издание, с две игри. Работи безупречно, с гаранция до края на годината.", 950),
    (9, "Lego", "lego city set", "Lego City Полицейски участък", "Над 800 части, всички фигурки са налични. Подходящ за деца над 6 години.", 85),
    (9, "Lego", "lego classic bricks", "Lego Classic голяма кутия с тухлички", "Стотици цветни части за свободно строене. Измити и преброени.", 40),
    (9, "Sony", "sony playstation controller", "Джойстик Sony DualSense бял", "Без дрейф на аналоговите стикове, батерията издържа цял ден.", 80),
    # 10 Фотография
    (10, "Canon", "canon dslr camera", "Фотоапарат Canon EOS 250D с обектив 18-55", "Само 4000 кадъра. Подходящ за начинаещи, с чанта и две батерии.", 720),
    (10, "Sony", "sony mirrorless camera", "Беззеркален фотоапарат Sony Alpha a6000", "Бърз автофокус и 24MP. Продавам само тялото със зарядно.", 640),
    (10, "Canon", "canon portrait lens", "Обектив Canon 50mm f/1.8 STM", "Остър и светъл, идеален за портрети. Без прах и гъбички.", 180),
    (10, "Hama", "hama camera tripod", "Статив Hama Star 61 с бързо захващане", "Лек, алуминиев, с чанта за носене. Издържа до 3 кг.", 38),
    (10, "Canon", "canon photo printer", "Фотопринтер Canon Selphy CP1300", "Печата снимки 10 на 15 за около минута. Три касети в комплекта.", 150),
    # 11 Красота и здраве
    (11, "Philips", "philips electric shaver", "Електрическа самобръсначка Philips Series 5000", "Мокро и сухо бръснене с три глави. Включени са калъф и почистваща четка.", 90),
    (11, "Dyson", "dyson hair dryer", "Сешоар Dyson Supersonic", "Бързо суши и пази косата. Със 4 приставки и калъф.", 450),
    (11, "Xiaomi", "xiaomi fitness band", "Фитнес гривна Xiaomi Mi Band 7", "Следи сън, пулс и тренировки. Включени са две допълнителни каишки.", 55),
    (11, "Philips", "philips electric toothbrush", "Електрическа четка за зъби Philips Sonicare", "Три режима на работа, с пътна кутия и зарядна станция.", 70),
    (11, "Philips", "philips air purifier", "Пречиствател на въздух Philips 2000i", "За стаи до 50 кв.м., с филтър, сменен преди два месеца.", 310),
    # 12 Градина и инструменти
    (12, "Bosch", "bosch cordless drill", "Акумулаторна бормашина Bosch GSR 18V", "С две батерии, зарядно и куфар. Мощна и лека, използвана за ремонт на апартамент.", 230),
    (12, "Bosch", "bosch lawn mower", "Електрическа косачка Bosch Rotak 1200", "Коси до 400 кв.м. трева, с кош за събиране. Ножът е наскоро заточен.", 190),
    (12, "Bosch", "bosch circular saw", "Циркуляр Bosch GKS 190", "Точен рез, с водач и два диска. Подходящ за дърводелски работи.", 170),
    (12, "Decathlon", "decathlon garden barbecue", "Градинска скара Decathlon на въглища", "Голяма решетка, с капак и термометър. Стояла е под навес.", 120),
    (12, "Bosch", "bosch pressure washer", "Водоструйка Bosch Easy Aquatak 120", "Идеална за миене на кола и тераси, с маркуч от 6 метра.", 150),
    # 13 Умна електроника
    (13, "Xiaomi", "xiaomi robot vacuum", "Прахосмукачка робот Xiaomi Robot Vacuum S10", "Лазерна навигация и управление през приложение. Контейнерът е почистен.", 340),
    (13, "Samsung", "samsung smartwatch", "Смарт часовник Samsung Galaxy Watch 5", "Следи здравето и спорта, с GPS. Включено е оригиналното зарядно.", 270),
    (13, "Xiaomi", "xiaomi smart camera", "Охранителна камера Xiaomi Smart Camera C300", "2K резолюция, нощно виждане, въртяща се глава. Работи с приложение.", 48),
    (13, "Samsung", "samsung portable ssd", "Външен SSD диск Samsung T7 1TB", "Много бърз, с USB-C и кабел. Използван за резервни копия.", 130),
    (13, "Asus", "asus wifi router", "Wi-Fi рутер Asus RT-AX55", "Wi-Fi 6, покрива цял тристаен апартамент. Лесна настройка.", 85),
]
PRODUCT_CLOSERS = [
    "Възможна е огледна среща в центъра на града.",
    "Предавам лично или с куриер за сметка на купувача.",
    "Цената е крайна, но при лично вземане има малко място за пазарлък.",
    "Продава се заради преместване.",
    "Имам касова бележка и гаранционна карта.",
    "Пишете ми за повече снимки.",
    "Не го пазя без капаро.",
    "Пушачи и домашни любимци няма.",
]
VIEWS = ["front view", "side view", "closeup", "in use"]

CITIES = ["София", "Пловдив", "Варна", "Бургас", "Русе", "Стара Загора", "Плевен", "Велико Търново",
          "Благоевград", "Шумен", "Добрич", "Пазарджик", "Хасково", "Перник", "Ямбол", "Габрово", "Сливен"]
STREETS = ["ул. „Витоша“", "бул. „България“", "ул. „Гео Милев“", "ул. „Цар Освободител“", "бул. „Христо Ботев“",
           "ул. „Раковски“", "ул. „Алеко Константинов“", "бул. „Цариградско шосе“", "ул. „Хан Аспарух“",
           "ул. „Княз Борис I“", "ул. „Шипка“", "бул. „Александровска“", "ул. „Патриарх Евтимий“", "ул. „Оборище“"]
RECEIVERS = ["Георги Петров", "Мария Иванова", "Иван Димитров", "Петър Стоянов", "Елена Георгиева",
             "Николай Тодоров", "Десислава Колева", "Стефан Маринов", "Виктория Николова", "Борис Христов",
             "Анна Йорданова", "Калоян Василев", "Радослава Атанасова", "Мартин Ангелов", "Силвия Павлова",
             "Тодор Банков", "Йоана Симеонова", "Димитър Кирилов", "Габриела Митева", "Ясен Панайотов"]

# ----------------------------------------------------------------------------------------------
# Posts: 30 hand-written general posts; 30 more are generated from products (below)
# ----------------------------------------------------------------------------------------------
GENERAL_POSTS = [
    ("Как да избегнем измами при онлайн покупки", "Винаги искайте да видите артикула на живо, преди да платите. Най-сигурно е плащането при получаване, а при по-скъпи покупки - среща на публично място."),
    ("Търся мнения за безжични слушалки", "Колебая се между два модела до 200 лева. Ползвам ги основно за пътуване с влак и в офиса. Какво бихте препоръчали?"),
    ("Първата ми продажба в платформата мина успешно", "Продадох стария си телефон за по-малко от час. Благодаря на всички, които се свързаха с мен, и на купувача за коректността."),
    ("Как опаковате чупливи неща за куриер?", "Изпращам за първи път стъклена ваза. Използвам балони и картонена кутия, но не съм сигурен дали е достатъчно. Споделете опита си."),
    ("Търся колело за дете на 7 години", "Бюджетът ми е около 200 лева. Предпочитам употребявано, но в добро състояние. Живея във Варна и мога да го взема лично."),
    ("Преместване и разчистване - всичко отива в добри ръце", "След ремонта в апартамента останаха мебели и дреболии. Ще качвам по няколко обяви всяка седмица, така че следете профила ми."),
    ("Струва ли си да сменя диска на стар лаптоп?", "Лаптопът ми е на 6 години и се бави при стартиране. Мисля дали да сложа SSD или да купя нов. Има ли опит от някого?"),
    ("Съвети за покупка на употребяван телефон", "Проверявайте батерията, камерите и дали не е заключен за оператор. Попитайте за кутия и касова бележка - това е добър знак."),
    ("Среща на любителите на фотографията", "В събота в Борисовата градина правим излет със снимки. Всеки може да донесе своя апарат и да сподели трикове. Присъединете се!"),
    ("Благодаря на купувача от Пловдив", "Сделката мина бързо и коректно. Дано ползва шкафа с радост. Препоръчвам да търгувате със снимки и ясни описания - спестява много време."),
    ("Какво да правя с ненужни електроуреди?", "Имам стара микровълнова и тостер, които работят, но ми заемат място. По-добре да ги продам евтино или да ги подаря?"),
    ("Кой има опит с електрически тротинетки?", "Мисля да си взема за ежедневно пътуване до работа - около 8 км. Интересува ме реалният пробег и дали са удобни по нашите улици."),
    ("Лятна разпродажба на детски дрехи", "Събрахме добре запазени дрешки за деца от 2 до 5 години. Цените са символични. Пишете ми за размери и снимки."),
    ("Как се пазарим учтиво", "Лично предпочитам да предложа 10 процента по-малко и да изчакам. Учтивостта върши работа - повечето продавачи са готови на отстъпка."),
    ("Как да снимам по-добре продуктите за обява?", "Дневната светлина и неутралният фон правят чудеса. Снимайте от няколко ъгъла и покажете и дефектите, ако има такива."),
    ("Намерих перфектния диван!", "След месец търсене си купих диван на много добра цена. Благодаря за съветите от предишната ми публикация, помогнаха ми да не изпусна офертата."),
    ("Търся хладилник до 400 лева", "Нужен ми е за студентска квартира в София. Важното е да е работещ и тих. Предложения с цена и снимки са добре дошли."),
    ("Съвет: винаги питайте за гаранция", "Дори при употребявани уреди много продавачи имат остатъчна гаранция. Това е сигурност за вас и повече доверие към продавача."),
    ("Каталозите - ползвате ли ги?", "Започнах да събирам любими обяви в каталози и така сравнявам цените спокойно. Вие как си организирате търсенето?"),
    ("Подарък за приятел, любител на технологиите", "Бюджетът е около 150 лева. Мислех за смарт часовник или силна външна батерия. Какво според вас би зарадвало повече?"),
    ("Идеи за обзавеждане на малка кухня", "Кухнята ми е само 6 квадрата. Търся компактни, но функционални уреди и мебели. Споделете какво работи за вас."),
    ("Моят опит с доставки през куриер", "Общо взето всичко пристига навреме, но винаги проверявайте пратката при получаване. Снимайте опаковката, ако има видимо увреждане."),
    ("Спортуване у дома през зимата", "Започнах тренировки вкъщи с гири и постелка. Резултатите се виждат за два месеца. Някой друг да споделя програма?"),
    ("Колко време държите обявите си активни?", "Аз ги пазя около месец и после ги обновявам с нова снимка. Забелязах, че така получавам повече запитвания."),
    ("Хубави места за вело разходки около Бургас", "Крайбрежната алея и посоката към Сарафово са чудесни за неделна разходка. Ако някой иска да се присъедини, пишете."),
    ("Търся добър майстор за пералня", "Не изстисква добре и издава шум при центрофугата. Някой да препоръча коректен майстор в Русе?"),
    ("Наръчници за начинаещи фотографи", "Прочетох няколко добри наръчника за композиция и светлина. Ако искате заглавията, ще ги споделя в коментарите."),
    ("Продавам малко, но качествено", "По-добре да имам три обяви със страхотни снимки, отколкото тридесет с бегли описания. Така купувачите ми вярват повече."),
    ("Идеи за подаръци до 50 лева", "Наближават празниците и събирам идеи. Мисля за слушалки, фитнес гривна или настолна игра. Вашите предложения?"),
    ("Благодарствено писмо към общността", "Откакто съм тук, продадох и купих много неща и срещнах приятни хора. Благодаря за добрите отзиви и за търпението при пазарлъка."),
]
def mid(t):
    """Title for use mid-sentence: lower-case a leading Cyrillic word ("Тениска Puma" -> "тениска Puma")."""
    return t[0].lower() + t[1:] if t and "\u0400" <= t[0] <= "\u04ff" else t

POST_TITLE_TPL = ["Продавам {m}", "Нова обява: {t}", "Предлагам {m} на добра цена", "{t} - вече в платформата", "Разчиствам: {t}"]
POST_BODY_TPL = [
    "Качих обява за {m} на цена {p} лв. {d} Намирам се в {c}.",
    "Ако търсите нещо подобно, вижте обявата ми. {d} Цената е {p} лв. и мога да го предам лично в {c} или по куриер.",
    "След дълго колебание реших да продам {m}. {d} Искам {p} лв., намирам се в {c}.",
    "Освобождавам място и давам {m} за {p} лв. {d} Огледът е възможен в {c}.",
    "Коректна цена за състоянието - {p} лв. {d} Свържете се с мен, ако ви интересува. Град: {c}.",
]

# ----------------------------------------------------------------------------------------------
# Comments / reviews text banks
# ----------------------------------------------------------------------------------------------
GREET = ["", "", "", "Здравейте, ", "Добър ден, ", "Привет, ", "Здрасти, "]
COMMENT_BASES_PRODUCT = [
    # questions
    "още ли е наличен?", "каква е най-ниската цена?", "има ли възможност за доставка до {c}?",
    "може ли да изпратите още снимки?", "колко време е ползван?", "има ли гаранционна карта?",
    "работи ли без никакви проблеми?", "приемате ли плащане при получаване?", "бихте ли го разменили за нещо друго?",
    "възможна ли е среща в {c} в събота?", "до кога е актуална обявата?", "има ли следи от употреба, които не се виждат на снимките?",
    "в {c} ли се намирате или е възможно изпращане?", "какво е състоянието на батерията или износването?",
    # praise
    "страхотна обява, много подробно описание!", "много добра цена за това състояние.",
    "хубави снимки, веднага се вижда, че е поддържано.", "взех си подобно и съм много доволен.",
    "препоръчвам продавача, търгували сме и преди.", "точно каквото търсех, ще се свържа с вас.",
    "отлично представяне, така трябва да изглеждат всички обяви.", "много ми харесва, надявам се да не е продадено.",
    "виждам, че е в чудесен вид.", "от такива сделки всички печелим.",
    # haggling
    "ще свалите ли малко цената при лично вземане?", "давам {x} лв., ако още е актуално.",
    "мога да дойда днес в {c} и да платя в брой, става ли {x} лв.?", "предлагам {x} лв. и взимам веднага.",
]
COMMENT_BASES_GENERIC = [
    "благодаря за информацията!", "много полезно, запазвам си го.", "съгласен съм напълно.",
    "и аз имам подобен опит - всичко мина гладко.", "интересна гледна точка, не бях се замислял.",
    "дано повече хора пишат така подробно.", "поздравления за инициативата!",
    "същото ми се случи миналия месец, затова съветът е ценен.", "ще го споделя с приятели.",
    "най-добрият съвет, който съм чел тук напоследък.", "дано има още такива публикации.",
    "аз бих добавил, че винаги е добре да се огледа на място.", "с радост ще помогна, ако е нужно.",
    "не съм мислил за това, но звучи логично.", "ето така се прави общност!",
    "пиша ти лично, имам предложение.", "и при мен в {c} е така, потвърждавам.", "много точно казано.",
    "браво, точно такива публикации са полезни.", "наистина си струва да се обмисли.",
    "имам познат в {c}, който може да помогне - пиши ми.", "аз го правя по същия начин и работи.",
    "ще пробвам този подход още днес.", "благодаря, че споделяш опита си.", "добра идея, давам ѝ положителен глас.",
]
COMMENT_TAILS = ["", "", "", "", " Поздрави!", " Благодаря предварително.", " Чакам отговор.", " Хубав ден!", " Успех с продажбата!", " Лек ден."]

REVIEW_BY_STARS = {
    5: ["{t} е точно както е описан. Продавачът беше коректен и бърз.", "Страхотна покупка - {m} е в по-добро състояние от очакваното.",
        "Много съм доволен от {m}. Сделката мина гладко и без излишно чакане.", "Препоръчвам! {t} работи безупречно, а комуникацията беше приятна.",
        "Отлично качество за тази цена. Продавачът изпрати {m} още същия ден.", "Всичко беше както на снимките. Благодаря за бързата и честна сделка!",
        "{t} надмина очакванията ми. Ще купувам пак от същия продавач.", "Перфектно опакован и изпратен навреме. Пълна препоръка."],
    4: ["{t} е добър, има само дребни следи от употреба, които са посочени в описанието.", "Доволен съм от покупката, но доставката забави един ден.",
        "Като цяло много добре. {t} работи, както трябва, макар че кутията беше леко смачкана.", "Добра цена и коректен продавач. Бих искал малко повече снимки.",
        "Хубав продукт за парите. Малко беше по-скъпо от подобни обяви, но си заслужава.", "Всичко е наред, остава само да го ползвам по-дълго, за да съм сигурен."],
    3: ["{t} е приемлив, но състоянието е по-слабо от описаното.", "Нормална сделка - нито лоша, нито отлична. Очаквах по-добра опаковка.",
        "Работи, но има повече следи от износване, отколкото личеше на снимките."],
    2: ["Не съм особено доволен - {m} имаше проблем, който не беше споменат в обявата.", "Доставката беше бавна и продуктът не беше съвсем като в описанието."],
    1: ["За съжаление {m} дойде с повреда. Свързах се с продавача и чакам решение."],
}
REVIEW_TAILS = ["", "", " Благодаря!", " Препоръчвам.", " Ще купувам отново.", " Всичко е наред."]

CATALOGUE_NAMES = [
    "Любими за лятото", "Подаръци за рождени дни", "За домашния офис", "Мечтаният ми гардероб", "Техника за кухнята",
    "Спорт и активен отдих", "За новото жилище", "Идеи за коледни подаръци", "Бюджетни находки", "Гейминг ъгълът",
    "Всичко за колоездачите", "За фотографи любители", "Обзавеждане за хола", "Градина и ремонти", "Детски радости",
    "Слушалки и колонки", "Ученически списък", "За студентската квартира", "Умният дом", "Маратонки и тренировки",
    "Подарък за мама", "Подарък за татко", "Походи и къмпинг", "Тонове стил", "Кафе и уют",
    "Техника от висок клас", "Без пари, но с вкус", "Нещата за уикенда", "Мобилният ми свят", "За красота и здраве",
    "Преди заплата", "След заплата", "Ремонтна листа", "Колекция на месеца", "Следя цената",
    "Купувам през пролетта", "За малки пространства", "Любими марки", "Препоръки на приятели", "За работа и учене",
    "Празнично настроение", "Да не забравя", "Топ избор", "Планове за есента", "Неделен списък",
    "Нов старт", "За морето", "Тихи вечери", "Моментни хрумвания", "Дългосрочни планове",
]
ORDER_STATUS = ["Zavurshena"] * 9 + ["Obrabotva_se"] * 5 + ["Nova"] * 4 + ["Otkazana"] * 2

# ----------------------------------------------------------------------------------------------
# Build the seed
# ----------------------------------------------------------------------------------------------
def user_ref(i): return f"$seedUser:{i}"

seed = {}
seed["Brand"] = [{"refKey": f"brand-{i+1}", "Name": n} for i, n in enumerate(BRANDS)]
seed["Category"] = [{"refKey": f"cat-{i+1}", "Name": n} for i, n in enumerate(CATEGORIES)]

# ---- Products ----
owners = [i % N_USERS for i in range(len(PRODUCTS))]
rng.shuffle(owners)
products, product_images = [], {}   # product_images[row] = (n_images)
for idx, (ci, brand, en, title, desc, price) in enumerate(PRODUCTS, start=1):
    status = rng.choices(["Nalichen", "Zapazen", "Prodaden"], weights=[70, 15, 15])[0]
    qty = 0 if status == "Prodaden" else rng.choice([1, 1, 1, 2, 3, 5, 8, 12, 20, 35, 60])
    full_desc = desc + (" " + rng.choice(PRODUCT_CLOSERS) if rng.random() < 0.65 else "")
    n_img = rng.choices([2, 3, 4], weights=[30, 40, 30])[0]
    product_images[idx] = n_img
    products.append({
        "refKey": f"prod-{idx}",
        "parentRef_Brand": f"brand-{BRANDS.index(brand)+1}",
        "parentRef_Category": f"cat-{ci+1}",
        "Title": title, "Description": full_desc, "Price": float(price), "Quantity": qty,
        "Status": status, "UserId": user_ref(owners[idx - 1]),
        "imageKeywords": [f"{en} {VIEWS[j]}" for j in range(n_img)],
    })
seed["Product"] = products
P = {i + 1: p for i, p in enumerate(products)}            # row -> product dict
def img_path(row, j=1): return f"images/products/product-{row}-{j}.jpg"
def title_of(row): return P[row]["Title"]

# ---- Posts ----
posts, post_is_product = [], {}
author_cycle = [i % N_USERS for i in range(60)]; rng.shuffle(author_cycle)
linked_rows = rng.sample(range(1, len(PRODUCTS) + 1), 30)
entries = [("general", g) for g in GENERAL_POSTS] + [("product", r) for r in linked_rows]
rng.shuffle(entries)
for n, (kind, payload) in enumerate(entries, start=1):
    if kind == "general":
        title, content = payload
        image = img_path(rng.randint(1, len(PRODUCTS)), 1) if rng.random() < 0.15 else None
    else:
        row = payload; p = P[row]
        t = p["Title"]; city = rng.choice(CITIES)
        first = p["Description"].split(". ")[0].rstrip(".") + "."
        title = POST_TITLE_TPL[n % len(POST_TITLE_TPL)].format(t=t, m=mid(t))[:120]
        content = POST_BODY_TPL[rng.randrange(len(POST_BODY_TPL))].format(t=t, m=mid(t), p=int(p["Price"]), d=first, c=city)
        image = img_path(row, 1) if rng.random() < 0.85 else None
    row_ = {"refKey": f"post-{n}", "Title": title, "Content": content, "UserId": user_ref(author_cycle[n - 1])}
    if image: row_["ImagePath"] = image
    posts.append(row_)
    post_is_product[n] = (kind == "product")
seed["Post"] = posts
post_author = {i + 1: int(p["UserId"].split(":")[1]) for i, p in enumerate(posts)}

# ---- PostComment (skewed: a few hot posts, some silent) ----
used_comment_texts, comments = set(), []
post_weights = [rng.paretovariate(1.6) for _ in posts]
def comment_text(product_post):
    for _ in range(400):
        bank = COMMENT_BASES_PRODUCT if product_post and rng.random() < 0.75 else COMMENT_BASES_GENERIC
        base = rng.choice(bank).format(c=rng.choice(CITIES), x=rng.choice([10, 15, 20, 25, 30, 40, 50, 80, 100, 150]))
        g = rng.choice(GREET)
        text = (g + base if g else base[0].upper() + base[1:]) + rng.choice(COMMENT_TAILS)
        if text not in used_comment_texts:
            used_comment_texts.add(text); return text
    raise RuntimeError("could not build a unique comment")
for n in range(1, 241):
    pid = rng.choices(range(1, len(posts) + 1), weights=post_weights)[0]
    author = rng.randrange(N_USERS)
    if author == post_author[pid] and rng.random() < 0.8:      # authors mostly comment on others' posts
        author = (author + rng.randint(1, N_USERS - 1)) % N_USERS
    c = {"parentRef_Post": f"post-{pid}", "Content": comment_text(post_is_product[pid]), "UserId": user_ref(author)}
    if rng.random() < 0.06: c["ImagePath"] = img_path(rng.randint(1, len(PRODUCTS)), 1)
    comments.append(c)
seed["PostComment"] = comments

# ---- PostVote (unique post/user pairs, authors don't vote on own post) ----
votes = []
for pid in range(1, len(posts) + 1):
    popularity = rng.uniform(0.25, 0.95)
    for u in range(N_USERS):
        if u == post_author[pid] or rng.random() > popularity: continue
        votes.append({"parentRef_Post": f"post-{pid}", "UserId": user_ref(u), "Value": 1 if rng.random() < 0.85 else -1})
seed["PostVote"] = votes

# ---- Catalogues + items ----
catalogues, cat_items = [], []
names = CATALOGUE_NAMES[:]; rng.shuffle(names)
cn = 0
for u in range(N_USERS):
    for k in range(8):
        cn += 1
        catalogues.append({"refKey": f"catalogue-{cn}", "Name": names[cn - 1], "UserId": user_ref(u), "IsPublic": rng.random() < 0.7})
        picks = rng.sample(range(1, len(PRODUCTS) + 1), rng.randint(2, 5))
        for pos, prow in enumerate(picks, start=1):
            cat_items.append({"parentRef_Catalogue": f"catalogue-{cn}", "parentRef_Product": f"prod-{prow}", "Position": pos})
seed["Catalogue"], seed["CatalogueItem"] = catalogues, cat_items

# ---- Cart ----
available = [r for r, p in P.items() if p["Status"] == "Nalichen"]
cart = []
for u, n_items in zip(range(N_USERS), [3, 7, 6, 8, 5, 3]):
    mine = [r for r in available if int(P[r]["UserId"].split(":")[1]) != u]
    for prow in rng.sample(mine, n_items):
        cart.append({"UserId": user_ref(u), "parentRef_Product": f"prod-{prow}", "Quantity": rng.choice([1, 1, 1, 2, 2, 3])})
seed["CartItem"] = cart

# ---- Orders + items ----
orders, order_items = [], []
per_user_orders = [4, 8, 8, 8, 6, 4]
on = 0
for u, count in enumerate(per_user_orders):
    for _ in range(count):
        on += 1
        status = rng.choice(ORDER_STATUS)
        pool = available if status in ("Nova", "Obrabotva_se") else list(P)
        pool = [r for r in pool if int(P[r]["UserId"].split(":")[1]) != u]
        picks = rng.sample(pool, rng.randint(1, 4))
        total = 0.0
        for prow in picks:
            q = rng.choice([1, 1, 1, 2]) if P[prow]["Price"] > 100 else rng.choice([1, 1, 2, 3])
            up = P[prow]["Price"]; total += q * up
            order_items.append({"parentRef_Order": f"order-{on}", "parentRef_Product": f"prod-{prow}", "Quantity": q, "UnitPrice": up})
        orders.append({"refKey": f"order-{on}", "UserId": user_ref(u), "Total": round(total, 2),
                       "ReceiverName": rng.choice(RECEIVERS),
                       "DeliveryAddress": f"{rng.choice(STREETS)} {rng.randint(1, 140)}, {rng.choice(CITIES)}",
                       "Status": status})
seed["Order"], seed["OrderItem"] = orders, order_items

# ---- Follows: 26 of the 30 possible directed pairs (6 users), always including mutual pairs ----
pairs = [(a, b) for a in range(N_USERS) for b in range(N_USERS) if a != b]
rng.shuffle(pairs)
keep = pairs[:26]
for must in [(0, 1), (1, 0), (2, 3), (3, 2)]:
    if must not in keep: keep.append(must)
seed["Follow"] = [{"FollowerUserId": user_ref(a), "FollowedUserId": user_ref(b)} for a, b in sorted(keep)]

# ---- Reviews (unique product/author pairs, never on own listing, skewed to 4-5 stars) ----
reviews, used_review_texts, used_pairs = [], set(), set()
def review_text(stars, row):
    for _ in range(200):
        text = rng.choice(REVIEW_BY_STARS[stars]).format(t=title_of(row), m=mid(title_of(row))) + rng.choice(REVIEW_TAILS)
        if text not in used_review_texts:
            used_review_texts.add(text); return text
    return rng.choice(REVIEW_BY_STARS[stars]).format(t=title_of(row), m=mid(title_of(row))) + f" Купих го в {rng.choice(CITIES)}."
while len(reviews) < 130:
    row = rng.randint(1, len(PRODUCTS)); u = rng.randrange(N_USERS)
    if u == int(P[row]["UserId"].split(":")[1]) or (row, u) in used_pairs: continue
    used_pairs.add((row, u))
    stars = rng.choices([5, 4, 3, 2, 1], weights=[50, 30, 12, 6, 2])[0]
    r = {"parentRef_Product": f"prod-{row}", "AuthorUserId": user_ref(u), "Stars": stars, "Text": review_text(stars, row)}
    if rng.random() < 0.10: r["ImagePath"] = img_path(row, 1)
    reviews.append(r)
seed["Review"] = reviews

# ----------------------------------------------------------------------------------------------
# Self-check: mirror SeedStage.BuildSeedSchema + VerifyStage.EnglishLeaks (stricter: scans raw values)
# ----------------------------------------------------------------------------------------------
def selfcheck():
    import jsonschema
    S = lambda t, **kw: {"type": t, **kw}
    def row(props, required, extra=None):
        return {"type": "object", "properties": {**props, **(extra or {})}, "required": required, "additionalProperties": False}
    s, n, i, b = S("string"), S("number"), S("integer"), S("boolean")
    kw_schema = S("array", items=s, minItems=2, maxItems=4)
    models = {
        "Brand": (row({"refKey": s, "Name": s}, ["refKey", "Name"]), 6),
        "Category": (row({"refKey": s, "Name": s}, ["refKey", "Name"]), 5),
        "Product": (row({"refKey": s, "parentRef_Brand": s, "parentRef_Category": s, "Title": s, "Description": s,
                         "Price": n, "Quantity": i, "Status": s, "UserId": s, "imageKeywords": kw_schema},
                        ["refKey", "parentRef_Brand", "parentRef_Category", "Title", "Description", "Price", "Quantity", "Status", "UserId", "imageKeywords"]), 15),
        "Post": (row({"refKey": s, "Title": s, "Content": s, "ImagePath": s, "UserId": s}, ["refKey", "Title", "Content", "UserId"]), 10),
        "PostComment": (row({"parentRef_Post": s, "Content": s, "UserId": s, "ImagePath": s}, ["parentRef_Post", "Content", "UserId"]), 8),
        "PostVote": (row({"parentRef_Post": s, "UserId": s, "Value": i}, ["parentRef_Post", "UserId", "Value"]), 12),
        "Catalogue": (row({"refKey": s, "Name": s, "UserId": s, "IsPublic": b}, ["refKey", "Name", "UserId", "IsPublic"]), 12),
        "CatalogueItem": (row({"parentRef_Catalogue": s, "parentRef_Product": s, "Position": i}, ["parentRef_Catalogue", "parentRef_Product", "Position"]), 6),
        "CartItem": (row({"UserId": s, "parentRef_Product": s, "Quantity": i}, ["UserId", "parentRef_Product", "Quantity"]), 3),
        "Order": (row({"refKey": s, "UserId": s, "Total": n, "ReceiverName": s, "DeliveryAddress": s, "Status": s},
                      ["refKey", "UserId", "Total", "ReceiverName", "DeliveryAddress", "Status"]), 2),
        "OrderItem": (row({"parentRef_Order": s, "parentRef_Product": s, "Quantity": i, "UnitPrice": n}, ["parentRef_Order", "parentRef_Product", "Quantity", "UnitPrice"]), 3),
        "Follow": (row({"FollowerUserId": s, "FollowedUserId": s}, ["FollowerUserId", "FollowedUserId"]), 2),
        "Review": (row({"parentRef_Product": s, "AuthorUserId": s, "Stars": i, "Text": s, "ImagePath": s}, ["parentRef_Product", "AuthorUserId", "Stars"]), 2),
    }
    root = {"type": "object", "additionalProperties": False, "required": list(models),
            "properties": {k: {"type": "array", "items": v[0], "minItems": v[1]} for k, v in models.items()}}
    jsonschema.validate(seed, root)

    # referential integrity + business rules the schema can't express
    keys = {m: {r["refKey"] for r in seed[m]} for m in ("Brand", "Category", "Product", "Post", "Catalogue", "Order")}
    for m, ref in [("Product", "Brand"), ("Product", "Category"), ("PostComment", "Post"), ("PostVote", "Post"),
                   ("CatalogueItem", "Catalogue"), ("CatalogueItem", "Product"), ("CartItem", "Product"),
                   ("OrderItem", "Order"), ("OrderItem", "Product"), ("Review", "Product")]:
        for r in seed[m]: assert r[f"parentRef_{ref}"] in keys[ref], (m, ref, r)
    users = {user_ref(k) for k in range(N_USERS)}
    for m in seed:
        for r in seed[m]:
            for f, v in r.items():
                if f.endswith("UserId"): assert v in users, (m, f, v)
    assert len(set(p["Title"] for p in seed["Product"])) == len(seed["Product"])
    for fld, mx in [("Product", ("Title", 100)), ("Post", ("Title", 120)), ("PostComment", ("Content", 500))]:
        for r in seed[fld]: assert 3 <= len(r[mx[0]]) <= mx[1], (fld, r[mx[0]])
    for o in seed["Order"]:
        its = [x for x in seed["OrderItem"] if x["parentRef_Order"] == o["refKey"]]
        assert its and abs(sum(x["Quantity"] * x["UnitPrice"] for x in its) - o["Total"]) < 0.01
    assert all(r["Status"] in ("Nalichen", "Zapazen", "Prodaden") for r in seed["Product"])
    assert all(o["Status"] in ("Nova", "Obrabotva_se", "Zavurshena", "Otkazana") for o in seed["Order"])
    # every image path used by Post/PostComment/Review exists as a downloaded product image
    exist = {img_path(r, j) for r in P for j in range(1, product_images[r] + 1)}
    for m in ("Post", "PostComment", "Review"):
        for r in seed[m]:
            if "ImagePath" in r: assert r["ImagePath"] in exist, (m, r["ImagePath"])
    # uniqueness of descriptive strings (prompt rule 3)
    for m, f in [("Product", "Description"), ("Post", "Title"), ("Post", "Content"), ("PostComment", "Content"),
                 ("Catalogue", "Name"), ("Review", "Text")]:
        vals = [r[f] for r in seed[m]]
        assert len(vals) == len(set(vals)), f"duplicate {m}.{f}"
    # image keyword uniqueness + no keyword is a prefix of another (matters for --image-pack globbing)
    kws = [k for p in seed["Product"] for k in p["imageKeywords"]]
    assert len(kws) == len(set(kws))
    assert not any(a != b and b.startswith(a) for a in kws for b in kws)
    # Bulgarian-only-UI gate: same heuristic as VerifyStage.EnglishLeaks
    common = set("the and for with your you login log register password email submit search home about contact edit delete "
                 "create details add cart welcome please enter remember forgot account sign out in new back list save cancel required field".split())
    def walk(x):
        if isinstance(x, str): yield x
        elif isinstance(x, list):
            for y in x: yield from walk(y)
        elif isinstance(x, dict):
            for k, y in x.items():
                if k != "imageKeywords": yield from walk(y)
    leaks = [m.group(0) for t in walk(seed) for m in re.finditer(r"\b[A-Za-z]{2,}(?:\s+[A-Za-z]{2,})+\b", t)
             if any(w.lower() in common for w in m.group(0).split())]
    assert not leaks, f"English-leak heuristic would flag: {leaks[:10]}"

# ----------------------------------------------------------------------------------------------
# Images (Pillow). Cyrillic needs DejaVu; LaTeX isn't installed here and isn't needed for this.
# ----------------------------------------------------------------------------------------------
def slug(k): return "".join(c if c.isalnum() else "-" for c in k.lower())

def make_images():
    import colorsys
    from PIL import Image, ImageDraw, ImageFont
    FB, FR = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    W, H = 800, 600
    def font(path, size): return ImageFont.truetype(path, size)
    def hsv(h, s, v): return tuple(int(c * 255) for c in colorsys.hsv_to_rgb((h % 360) / 360, s, v))
    def wrap(d, text, f, width):
        lines, cur = [], ""
        for w in text.split():
            trial = (cur + " " + w).strip()
            if d.textlength(trial, font=f) <= width: cur = trial
            else: lines.append(cur); cur = w
        return lines + [cur]
    written = 0
    for row, p in P.items():
        ci = int(p["parentRef_Category"].split("-")[1]) - 1
        n = product_images[row]
        for j, kw in enumerate(p["imageKeywords"]):
            h = ci * (360 / len(CATEGORIES)) + j * 14
            top, bot = hsv(h, 0.45, 0.95), hsv(h + 28, 0.75, 0.42)
            img = Image.new("RGB", (W, H)); px = ImageDraw.Draw(img)
            for y in range(H):
                t = y / (H - 1)
                px.line([(0, y), (W, y)], fill=tuple(int(top[c] + (bot[c] - top[c]) * t) for c in range(3)))
            over = Image.new("RGBA", (W, H), (0, 0, 0, 0)); od = ImageDraw.Draw(over)
            if j == 0:
                for k in range(7): od.ellipse([60 + k * 95 - 90, 40 + (k % 3) * 160, 60 + k * 95 + 90, 220 + (k % 3) * 160], fill=(255, 255, 255, 26))
            elif j == 1:
                for k in range(-H, W, 46): od.line([(k, 0), (k + H, H)], fill=(255, 255, 255, 38), width=14)
            elif j == 2:
                for gx in range(30, W, 52):
                    for gy in range(30, H, 52): od.ellipse([gx - 6, gy - 6, gx + 6, gy + 6], fill=(255, 255, 255, 60))
            else:
                for r in range(60, 560, 70): od.ellipse([W // 2 - r, H // 2 - r, W // 2 + r, H // 2 + r], outline=(255, 255, 255, 50), width=6)
            od.rounded_rectangle([70, 150, W - 70, 450], radius=34, fill=(255, 255, 255, 214))
            img = Image.alpha_composite(img.convert("RGBA"), over); d = ImageDraw.Draw(img)
            dark = hsv(h + 28, 0.8, 0.28)
            d.text((W // 2, 205), p["parentRef_Brand"] and BRANDS[int(p["parentRef_Brand"].split("-")[1]) - 1], font=font(FB, 52), fill=dark, anchor="mm")
            tf = font(FB, 32); lines = wrap(d, p["Title"], tf, W - 190)[:3]
            y0 = 275 - (len(lines) - 1) * 22
            for k, ln in enumerate(lines): d.text((W // 2, y0 + k * 44), ln, font=tf, fill=(30, 30, 40), anchor="mm")
            d.text((W // 2, 420), f"Изглед {j + 1} от {n}", font=font(FR, 24), fill=(90, 90, 105), anchor="mm")
            d.text((34, 40), CATEGORIES[ci], font=font(FB, 28), fill=(255, 255, 255), anchor="lm")
            d.text((W - 34, H - 34), "ТЕСТОВО ИЗОБРАЖЕНИЕ", font=font(FR, 20), fill=(255, 255, 255), anchor="rm")
            d.text((34, H - 34), f"обява №{row}", font=font(FR, 20), fill=(255, 255, 255), anchor="lm")
            main = CACHE_DIR / f"pexels-{slug(kw)}.jpg"
            img.convert("RGB").save(main, "JPEG", quality=82)
            for prov in PROVIDERS[1:]:
                dst = CACHE_DIR / f"{prov}-{slug(kw)}.jpg"
                if dst.exists(): dst.unlink()
                try: os.link(main, dst)
                except OSError: shutil.copyfile(main, dst)
            written += 1
    return written

if __name__ == "__main__":
    selfcheck()
    SEED_OUT.parent.mkdir(exist_ok=True)
    SEED_OUT.write_text(json.dumps(seed, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    CACHE_DIR.mkdir(exist_ok=True)
    n_img = make_images()
    print(f"wrote {SEED_OUT.relative_to(ROOT)} ({SEED_OUT.stat().st_size // 1024} KiB)")
    for m, rows in seed.items(): print(f"  {m:<14}{len(rows):>5}")
    print(f"images: {n_img} unique -> .image-cache/ (x{len(PROVIDERS)} provider prefixes)")
