import os
import glob
import pathlib
import csv
import matplotlib.pyplot as plt
import numpy as np
from skimage import metrics
import cv2
import statistics
import re

values = {}
num_seeds = 0

def get_stddev():
    stdev = []
    
    for key in values:
        stdev.append(statistics.stdev(values[key]))

    return stdev

fps = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
coherences = np.arange(0.05, 0.65, 0.05)

def get_fps(file, it):
    with open(file, newline='') as csvfile:
        reader = csv.reader(csvfile, delimiter=',', quotechar='|')
        
        sum = 0
        amount = 0
        for row in reader:
            if not it in values:
                values[it] = []

            values[it].append(int(row[0]))
            sum += int(row[0])
            amount += 1

        fps[it] += sum / amount

def fps_walk(root):
    global coherences
    coherences = np.arange(0.0, 0.65, 0.05)

    it = 0
    for subdir, dirs, files in os.walk(root):
        for file in files:
            thresh, _, _ = re.split('_|.txt', file)
            loc = root + '/' + file
            index = float(thresh) / 0.05

            index = int(round(index))
            get_fps(loc, int(index))

def fps_result():
    actual_fps = fps

    stdev = get_stddev()

    #if (num_seeds != 0):
        #actual_fps = [x / num_seeds for x in fps]

    plt.errorbar(coherences, actual_fps, stdev)
    plt.plot(coherences, actual_fps)
    plt.xlabel('Coherence')
    plt.ylabel('FPS')
    plt.title('FPS based on Coherence')

    plt.savefig('fpscoh.png')

# SSIM
ssim = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0]

def_files = {}
all_files = {}

def ssim_diff(dir, def_dir, it):
    image = cv2.imread(dir)
    def_image = cv2.imread(def_dir)

    sim = metrics.structural_similarity(def_image, image, multichannel=True)
    ssim[it] += sim

    if not it in values:
        values[it] = []

    values[it].append(sim)
 
def ssim_walk_helper(root):
    for subdir, dirs, files in os.walk(root):
        for file in files:
            if (dir != 'img'):
                thresh, interp, pos, _ = re.split('_|.png', file)
                img_dir = root + '/' + file
                pos = int(pos)

                if (float(thresh) == 0.0):
                    def_files[pos] = file
                else:
                    if not pos in all_files:
                        all_files[pos] = []

                    all_files[pos].append(file)

def ssim_walk(root):
    global coherences
    coherences = np.arange(0.05, 0.65, 0.05)

    ssim_walk_helper(root)

    for i in range(1, 6):
        for file in all_files[i]:
            thresh, interp, pos, _ = re.split('_|.png', file)
            pos = int(pos)

            img_dir = root + '/' + file

            def_dir = root + '/' + def_files[pos]

            index = float(thresh) / 0.05
            index = int(round(index) - 1)
            ssim_diff(img_dir, def_dir, index)
        

def ssim_result():
    actual_ssim = [x / 5 for x in ssim]

    stdev = get_stddev()

    plt.errorbar(coherences, actual_ssim, stdev)
    plt.plot(coherences, actual_ssim)
    plt.xlabel('Coherence')
    plt.ylabel('SSIM Value')
    plt.title('SSIM based on Coherence')

    plt.savefig(str(pathlib.Path().resolve()) + '/graphs/coherence-ssim.png')

def print_single_ssim(first, second, root):
    first = cv2.imread(root + "/" + first)
    second = cv2.imread(root + "/" + second)

    print(metrics.structural_similarity(cv2.imread(first), cv2.imread(second), multichannel=True))

def ssim_reset():
    global values, ssim, num_seeds

    values = {}
    ssim = [0.0, 0.0, 0.0]
    num_seeds = 0
    plt.cla()

def main():
    root = str(pathlib.Path().resolve())

    # Get graph of FPS vs n
    fps_walk(root + '/coherence/fps')
    fps_result()

    #ssim_reset()
    #ssim_walk(root + '/coherence/img')
    #ssim_result()

if __name__ == "__main__":
    main()