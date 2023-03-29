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

fps = [0, 0, 0, 0]
thresholds = [2.0, 4.0, 8.0]

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
    global num_seeds

    it = 0
    for subdir, dirs, files in os.walk(root):
        for file in files:
            sseed, dseed, interp, _ = re.split('_|.txt', file)
            loc = root + '/' + file

            if (float(interp) == 0.0):
                num_seeds += 1
                it = 0
                get_fps(loc, it)
            else:
                it += 1
                get_fps(loc, it)

def fps_result():
    actual_fps = fps

    stdev = get_stddev()

    if (num_seeds != 0):
        actual_fps = [x / num_seeds for x in fps]

    plt.errorbar(thresholds, actual_fps, stdev)
    plt.plot(thresholds, actual_fps)
    plt.xlabel('nth ray marched')
    plt.ylabel('FPS difference')
    plt.title('FPS based on n')

    #plt.xticks(np.arange(0.2, 1.2, 0.2))

    plt.savefig('fps.png')

# SSIM
ssim = [0.0, 0.0, 0.0]
def_pos = "1"

def ssim_diff(dir, def_dir, it):
    image = cv2.imread(dir)
    def_image = cv2.imread(def_dir)

    sim = metrics.structural_similarity(def_image, image, multichannel=True)
    ssim[it] += sim

    if not it in values:
        values[it] = []

    values[it].append(sim)
 
def ssim_walk(root):
    global num_seeds
    def_dir = ''

    for subdir, dirs, files in os.walk(root):
        for file in files:
            if (dir != 'img'):
                sseed, dseed, interp, pos = file.split('_')
                img_dir = root + '/' + file

                if (pos != def_pos + ".png"):
                    continue
                
                if (float(interp) == 0.0):
                    it = 0
                    def_dir = img_dir
                    num_seeds += 1
                else:
                    ssim_diff(img_dir, def_dir, it)
                    it += 1
         

def ssim_result():
    actual_ssim = ssim

    stdev = get_stddev()

    if (num_seeds != 0):
        actual_ssim = [x / num_seeds for x in ssim]

    plt.errorbar(thresholds, actual_ssim, stdev)
    plt.plot(thresholds, actual_ssim)
    plt.xlabel('nth ray marched')
    plt.ylabel('SSIM Value')
    plt.title('SSIM')

    plt.savefig(str(pathlib.Path().resolve()) + '/graphs/pos-' + def_pos + '_ssim.png')

def print_single_ssim(first, second, root):
    first = cv2.imread(root + "/" + first)
    second = cv2.imread(root + "/" + second)

    print(metrics.structural_similarity(cv2.imread(first), cv2.imread(second), multichannel=True))

def ssim_reset(new_pos):
    global values, ssim, num_seeds, def_pos

    values = {}
    ssim = [0.0, 0.0, 0.0]
    num_seeds = 0
    def_pos = new_pos
    plt.cla()

def main():
    root = str(pathlib.Path().resolve())

    # Get graph of FPS vs n
    #fps_walk(root + '/fps')
    #fps_result()

    positions = 5

    for pos in range(1,positions+1):
        ssim_reset(str(pos))
        ssim_walk(root + '/img')
        ssim_result()

if __name__ == "__main__":
    main()