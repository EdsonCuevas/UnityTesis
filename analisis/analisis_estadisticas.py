"""
Análisis de las estadísticas del simulador VR.

Uso:
    python analisis_estadisticas.py <carpeta_con_csv> [--salida resultados] [--anonimizar claves_privadas.csv]

<carpeta_con_csv> es la carpeta "estadisticas" copiada del Quest
(Android/data/com.KamehouseO.VRElectricalSimulation/files/estadisticas). Puede contener varias
copias en subcarpetas (por ejemplo una por día o por visor): se juntan y se quitan los repetidos.

Genera en la carpeta de salida:
    intentos_limpios.csv   un renglón por intento, con número de intento por alumno
    pasos_limpios.csv      un renglón por paso
    resumen_alumnos.csv    por alumno, nivel, voltaje y modo
    resumen_pasos.csv      por paso: tiempo mediano, avisos y errores promedio
    *.png                  gráficas

El alumno se identifica con su número de cuenta (8 dígitos). Con --anonimizar cada número se cambia por un
código (A001, A002…) en todas las tablas y gráficas, y la equivalencia se guarda en el archivo indicado.
Ese archivo es privado: guárdalo aparte y no lo compartas. Si ya existe, se reutilizan sus códigos, así un
alumno conserva su código aunque el análisis se corra otra vez con datos nuevos.
"""

import argparse
from pathlib import Path

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
import pandas as pd

# Paleta: categórica en orden fijo, texto neutro, cuadrícula discreta.
SERIES = ["#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948"]
TEXT = "#0b0b0b"
TEXT_2 = "#52514e"
GRID = "#e4e3df"
INDIVIDUAL = "#c9c8c2"

MODE_ORDER = ["Tutorial", "Práctica", "Evaluación"]
RESULT_ORDER = ["completado", "abandonado", "saltado", "sin_cierre"]

NO_STUDENT = "sin_id"
ACCOUNT_DIGITS = 8

TEXT_COLS = {"intento_id": str, "alumno": str, "sesion": str, "nivel": str, "voltaje": str, "modo": str,
             "paso": str, "resultado": str}


def style():
    plt.rcParams.update({
        "figure.facecolor": "white",
        "axes.facecolor": "white",
        "axes.edgecolor": GRID,
        "axes.labelcolor": TEXT_2,
        "axes.titlecolor": TEXT,
        "axes.titlesize": 12,
        "axes.titleweight": "bold",
        "axes.titlelocation": "left",
        "axes.spines.top": False,
        "axes.spines.right": False,
        "axes.grid": True,
        "axes.axisbelow": True,
        "grid.color": GRID,
        "grid.linewidth": 0.8,
        "xtick.color": TEXT_2,
        "ytick.color": TEXT_2,
        "xtick.major.size": 0,
        "ytick.major.size": 0,
        "font.size": 10,
        "legend.frameon": False,
        "savefig.dpi": 200,
        "savefig.bbox": "tight",
    })


def read_all(folder: Path, prefix: str) -> pd.DataFrame:
    files = sorted(folder.rglob(f"{prefix}*.csv"))
    if not files:
        return pd.DataFrame()
    frames = [pd.read_csv(f, encoding="utf-8-sig", dtype=TEXT_COLS, keep_default_na=False) for f in files]
    return pd.concat(frames, ignore_index=True)


def load(folder: Path):
    steps = read_all(folder, "pasos")
    attempts = read_all(folder, "intentos")
    if steps.empty and attempts.empty:
        raise SystemExit(f"No hay pasos*.csv ni intentos*.csv en {folder}")

    steps = steps.drop_duplicates(subset=["intento_id", "paso_n", "terminado"])
    attempts = attempts.drop_duplicates(subset=["intento_id"])

    for col in ["segundos", "avisos", "errores", "paso_n", "terminado"]:
        steps[col] = pd.to_numeric(steps[col])
    steps["terminado"] = steps["terminado"].astype(bool)

    for col in ["segundos", "pasos_completados", "pasos_totales", "avisos", "errores"]:
        attempts[col] = pd.to_numeric(attempts[col])
    attempts["inicio"] = pd.to_datetime(attempts["inicio"])
    attempts["fin"] = pd.to_datetime(attempts["fin"])

    # Intentos que dejaron pasos pero no su renglón final (se apagó el visor o se cerró la app a la fuerza).
    missing = steps[~steps["intento_id"].isin(attempts["intento_id"])]
    if not missing.empty:
        rebuilt = (missing.groupby("intento_id")
                   .agg(alumno=("alumno", "first"), sesion=("sesion", "first"), nivel=("nivel", "first"),
                        voltaje=("voltaje", "first"), modo=("modo", "first"), segundos=("segundos", "sum"),
                        pasos_completados=("terminado", "sum"), avisos=("avisos", "sum"),
                        errores=("errores", "sum"))
                   .reset_index())
        rebuilt["resultado"] = "sin_cierre"
        attempts = pd.concat([attempts, rebuilt], ignore_index=True)

    attempts["voltaje"] = attempts["voltaje"].replace("", "-")
    steps["voltaje"] = steps["voltaje"].replace("", "-")

    # Número de intento de cada alumno en cada nivel, voltaje y modo, en orden de juego.
    attempts = attempts.sort_values(["alumno", "inicio", "sesion"], na_position="last")
    attempts["intento_n"] = attempts.groupby(["alumno", "nivel", "voltaje", "modo"]).cumcount() + 1
    steps = steps.merge(attempts[["intento_id", "intento_n", "resultado"]], on="intento_id", how="left")
    return steps, attempts


def check_ids(attempts: pd.DataFrame):
    """Avisa de identificadores que no son un número de cuenta válido (por ejemplo, datos de prueba)."""
    ids = attempts["alumno"].drop_duplicates()
    bad = ids[~ids.str.fullmatch(rf"\d{{{ACCOUNT_DIGITS}}}")]
    if not bad.empty:
        print(f"Aviso: {len(bad)} identificador(es) sin {ACCOUNT_DIGITS} dígitos: {', '.join(sorted(bad))}")


def anonymize(steps: pd.DataFrame, attempts: pd.DataFrame, key_path: Path):
    """Cambia cada número de cuenta por un código estable (A001…) y guarda la equivalencia en key_path."""
    if key_path.exists():
        keys = pd.read_csv(key_path, dtype=str, encoding="utf-8-sig", keep_default_na=False)
        mapping = dict(zip(keys["numero_cuenta"], keys["codigo"]))
    else:
        mapping = {}

    # Los alumnos nuevos reciben código en el orden en que jugaron por primera vez.
    for account in attempts.sort_values("inicio", na_position="last")["alumno"].drop_duplicates():
        if account != NO_STUDENT and account not in mapping:
            mapping[account] = f"A{len(mapping) + 1:03d}"

    key_path.parent.mkdir(parents=True, exist_ok=True)
    pd.DataFrame({"codigo": list(mapping.values()), "numero_cuenta": list(mapping.keys())})         .to_csv(key_path, index=False, encoding="utf-8-sig")
    for frame in (steps, attempts):
        frame["alumno"] = frame["alumno"].map(lambda a: mapping.get(a, a))
    print(f"Anonimizado: {len(mapping)} alumnos. Equivalencias (privadas) en {key_path.resolve()}")


def summaries(steps: pd.DataFrame, attempts: pd.DataFrame):
    done = attempts[attempts["resultado"] == "completado"]
    by_student = (attempts.groupby(["alumno", "nivel", "voltaje", "modo"])
                  .agg(intentos=("intento_id", "count"),
                       completados=("resultado", lambda r: (r == "completado").sum()))
                  .join(done.groupby(["alumno", "nivel", "voltaje", "modo"])
                        .agg(mejor_segundos=("segundos", "min"), mediana_segundos=("segundos", "median"),
                             avisos_promedio=("avisos", "mean"), errores_promedio=("errores", "mean")))
                  .reset_index())

    finished = steps[steps["terminado"]]
    by_step = (finished.groupby(["nivel", "voltaje", "modo", "paso_n", "paso"])
               .agg(n=("intento_id", "count"), mediana_segundos=("segundos", "median"),
                    avisos_promedio=("avisos", "mean"), errores_promedio=("errores", "mean"))
               .reset_index())
    stuck = (steps[~steps["terminado"]].groupby(["nivel", "voltaje", "modo", "paso_n"])
             .size().rename("abandonos_en_paso").reset_index())
    by_step = by_step.merge(stuck, on=["nivel", "voltaje", "modo", "paso_n"], how="left")
    by_step["abandonos_en_paso"] = by_step["abandonos_en_paso"].fillna(0).astype(int)
    return by_student.round(2), by_step.round(2)


def label(nivel, voltaje):
    return nivel if voltaje == "-" else f"{nivel} {voltaje} V"


def file_part(nivel, voltaje, mode=None):
    name = nivel if voltaje == "-" else f"{nivel}_{voltaje}"
    if mode and mode.lower() != nivel:
        name += "_" + mode.lower().replace("á", "a").replace("ó", "o")
    return name


def fmt_minutes(ax):
    ax.yaxis.set_major_formatter(matplotlib.ticker.FuncFormatter(lambda s, _: f"{int(s // 60)}:{int(s % 60):02d}"))


def plot_learning_curves(attempts: pd.DataFrame, out: Path):
    """Tiempo total por número de intento: una línea gris por alumno y la mediana del grupo en azul."""
    done = attempts[(attempts["resultado"] == "completado") & (attempts["modo"] != "Tutorial")]
    for (nivel, voltaje), data in done.groupby(["nivel", "voltaje"]):
        modes = [m for m in MODE_ORDER if m in set(data["modo"])]
        fig, axes = plt.subplots(1, len(modes), figsize=(5 * len(modes), 4), sharey=True, squeeze=False,
                                 layout="constrained")
        for ax, mode in zip(axes[0], modes):
            d = data[data["modo"] == mode]
            for _, s in d.groupby("alumno"):
                ax.plot(s["intento_n"], s["segundos"], color=INDIVIDUAL, linewidth=1, marker="o", markersize=3)
            median = d.groupby("intento_n")["segundos"].median()
            ax.plot(median.index, median.values, color=SERIES[0], linewidth=2, marker="o", markersize=5)
            ax.set_title(mode)
            ax.set_xlabel("Número de intento")
            last = int(d["intento_n"].max())
            ax.set_xticks(range(1, last + 1))
            ax.set_xlim(0.5, last + 0.5)
        axes[0][0].set_ylabel("Tiempo total (min:s)")
        fmt_minutes(axes[0][0])
        handles = [plt.Line2D([], [], color=INDIVIDUAL, linewidth=1, marker="o", markersize=3, label="Cada alumno"),
                   plt.Line2D([], [], color=SERIES[0], linewidth=2, marker="o", markersize=5,
                              label="Mediana del grupo")]
        axes[0][-1].legend(handles=handles, loc="upper right")
        fig.suptitle(f"Curva de aprendizaje · {label(nivel, voltaje)}", x=0.01, ha="left",
                     fontweight="bold", color=TEXT)
        fig.savefig(out / f"curva_aprendizaje_{file_part(nivel, voltaje)}.png")
        plt.close(fig)


def plot_mode_comparison(attempts: pd.DataFrame, out: Path):
    """Tiempos de intentos completados por nivel: práctica contra evaluación, con cada intento como punto."""
    done = attempts[(attempts["resultado"] == "completado") & attempts["modo"].isin(["Práctica", "Evaluación"])]
    if done.empty:
        return
    groups = sorted({(n, v) for n, v in zip(done["nivel"], done["voltaje"])})
    fig, ax = plt.subplots(figsize=(max(5, 2.2 * len(groups)), 4))
    width = 0.35
    for m, mode in enumerate(["Práctica", "Evaluación"]):
        color = SERIES[m]
        for g, (nivel, voltaje) in enumerate(groups):
            values = done[(done["nivel"] == nivel) & (done["voltaje"] == voltaje) & (done["modo"] == mode)]["segundos"]
            if values.empty:
                continue
            x = g + (m - 0.5) * width
            ax.boxplot(values, positions=[x], widths=width * 0.7, showfliers=False, patch_artist=True,
                       boxprops=dict(facecolor="white", edgecolor=color, linewidth=1.5),
                       medianprops=dict(color=color, linewidth=2),
                       whiskerprops=dict(color=color, linewidth=1), capprops=dict(color=color, linewidth=1))
            jitter = [(i % 7 - 3) * width * 0.06 for i in range(len(values))]
            ax.scatter([x + j for j in jitter], values, s=14, color=color, alpha=0.6, linewidths=0, zorder=3)
        ax.scatter([], [], color=color, s=30, label=mode)
    ax.set_xticks(range(len(groups)), [label(n, v) for n, v in groups])
    ax.set_ylabel("Tiempo total (min:s)")
    fmt_minutes(ax)
    ax.grid(axis="x", visible=False)
    ax.legend(loc="upper left")
    ax.set_title("Tiempo por nivel: práctica contra evaluación")
    fig.savefig(out / "practica_vs_evaluacion.png")
    plt.close(fig)


def plot_steps(by_step: pd.DataFrame, out: Path):
    """Por nivel: tiempo mediano y avisos promedio de cada paso, para ver los pasos más difíciles."""
    for (nivel, voltaje, mode), d in by_step.groupby(["nivel", "voltaje", "modo"]):
        d = d.sort_values("paso_n")
        names = [f"{int(n)}. {p}" for n, p in zip(d["paso_n"], d["paso"])]
        height = max(3, 0.28 * len(d) + 1.2)
        fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(11, height + 0.4), sharey=True, layout="constrained",
                                       gridspec_kw={"width_ratios": [3, 2]})
        y = range(len(d))
        ax1.barh(y, d["mediana_segundos"], color=SERIES[0], height=0.7)
        ax1.set_yticks(list(y), names)
        ax1.invert_yaxis()
        ax1.set_xlabel("Tiempo mediano (s)")
        ax1.grid(axis="y", visible=False)
        ax1.set_title("Tiempo por paso")
        ax2.barh(y, d["avisos_promedio"], color=SERIES[1], height=0.7)
        ax2.set_xlabel("Avisos promedio por intento")
        ax2.grid(axis="y", visible=False)
        ax2.set_title("Avisos (dificultades)")
        fig.suptitle(f"Pasos · {label(nivel, voltaje)} · {mode}", x=0.01, ha="left", fontweight="bold", color=TEXT)
        fig.savefig(out / f"pasos_{file_part(nivel, voltaje, mode)}.png")
        plt.close(fig)


def plot_results(attempts: pd.DataFrame, out: Path):
    """Cuántos intentos se completaron, se abandonaron o se saltaron, por nivel."""
    attempts = attempts.assign(grupo=[f"{label(n, v)}\n{m}" for n, v, m in
                                      zip(attempts["nivel"], attempts["voltaje"], attempts["modo"])])
    table = attempts.pivot_table(index="grupo", columns="resultado", values="intento_id", aggfunc="count",
                                 fill_value=0)
    cols = [r for r in RESULT_ORDER if r in table.columns]
    table = table[cols]
    fig, ax = plt.subplots(figsize=(max(5, 1.3 * len(table)), 4))
    bottom = [0] * len(table)
    for i, col in enumerate(cols):
        ax.bar(range(len(table)), table[col], bottom=bottom, color=SERIES[i], width=0.6, label=col,
               edgecolor="white", linewidth=2)
        bottom = [b + v for b, v in zip(bottom, table[col])]
    ax.set_xticks(range(len(table)), table.index, fontsize=8)
    ax.set_ylabel("Intentos")
    ax.yaxis.set_major_locator(matplotlib.ticker.MaxNLocator(integer=True))
    ax.grid(axis="x", visible=False)
    ax.legend(loc="upper left", bbox_to_anchor=(1, 1))
    ax.set_title("Resultado de los intentos")
    fig.savefig(out / "resultado_intentos.png")
    plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("carpeta", type=Path, help="Carpeta con pasos.csv e intentos.csv")
    parser.add_argument("--salida", type=Path, default=Path("resultados"), help="Carpeta para tablas y gráficas")
    parser.add_argument("--anonimizar", type=Path, metavar="CLAVES_CSV",
                        help="Cambia los números de cuenta por códigos y guarda la equivalencia en este archivo")
    args = parser.parse_args()

    style()
    args.salida.mkdir(parents=True, exist_ok=True)
    steps, attempts = load(args.carpeta)
    check_ids(attempts)
    if args.anonimizar:
        if args.anonimizar.resolve().parent == args.salida.resolve():
            raise SystemExit("Guarda las claves fuera de la carpeta de resultados, para no compartirlas por error.")
        anonymize(steps, attempts, args.anonimizar)
    by_student, by_step = summaries(steps, attempts)

    attempts.to_csv(args.salida / "intentos_limpios.csv", index=False, encoding="utf-8-sig")
    steps.to_csv(args.salida / "pasos_limpios.csv", index=False, encoding="utf-8-sig")
    by_student.to_csv(args.salida / "resumen_alumnos.csv", index=False, encoding="utf-8-sig")
    by_step.to_csv(args.salida / "resumen_pasos.csv", index=False, encoding="utf-8-sig")

    plot_learning_curves(attempts, args.salida)
    plot_mode_comparison(attempts, args.salida)
    plot_steps(by_step, args.salida)
    plot_results(attempts, args.salida)

    print(f"{attempts['alumno'].nunique()} alumnos, {len(attempts)} intentos, {len(steps)} pasos.")
    print(f"Resultados en {args.salida.resolve()}")


if __name__ == "__main__":
    main()
