Offline CLI vyhledávátko spojení v jízdních řádech
==================================================

Program, který jako parametry dostane zdrojové a cílové místo, případně čas
odjezdu/příjezdu či další parametry a vypíše hezky formátovaný seznam spojení.

Vlastnosti:

  * vyhledání spojení
      - zadání z, do, přes
      - zadání času odjezdu či příjezdu
      - vrátí obvyklý IDOS-like výsledek, tedy spojení setříděná vzestupně
        podle času příjezdu, pro každý čas příjezdu vybere to s nejpozdějším
        časem odjezdu
      - zohlednění minimálního času na přestup
      - zohlednění pěších přesunů mezi blízkými zastávkami (alespoň na základě
        vzdálenosti vzdušnou čarou)
  * zkrácené zadávání názvů zastávek ala IDOS (e.g. "pra sm" = Praha-Smíchov)
  * vlastní zkratky pro často používané zastávky (e.g. ms = Malostranské nám.)
  * možnost definice virtuálních zastávek (e.g. Kolej = {Kuchyňka - 5min,
    Trojská - 8min, NádrHol - 12min)
  * import dat z následujících zdrojů
      - GTFS data publikovaná DPP (negarantuji obecnou podporu GTFS nad rámec
        toho, co aktuálně používá DPP) (ftp://jrdata:jrdata15@ftp.dpp.cz/)
      - KANGO soubory pro vlaky publikované Chapsem
        (ftp://ftp.cisjr.cz/draha/celostatni)
  * generování dat pro tab completion názvů zastávek v alespoň jednom shellu
    (pravděpodobně fish, ale možná i bash)
  * určený pro běh v prostředí Mono pod OS Linux

